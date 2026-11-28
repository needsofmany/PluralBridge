-- Deploy dbo.pb_show_tree_step5_graph_expansion to accept explicit inputs and return one result set.
-- This script has not been executed.
-- Run it after 013_graph_edge_table_types.sql and before 015_profile_graph_expansion_capture.sql,
-- before 016_step4_graph_expansion_capture.sql, and before the application build.
-- Until those callers are deployed, a bare EXEC fails because @InputContractVersion
-- has no default, or it returns a result set that the caller does not capture.
-- The procedure body matches scripts/sqlserver/pb_show_tree_step5_graph_expansion.sql.
-- Measured first-call delay was accompanied by compilation events. The exact waits
-- in that delay were not identified. This script does not grant permissions.
-- The calling login needs EXECUTE on this procedure and REFERENCES on
-- dbo.pb_WalkTopologyRows, dbo.pb_GraphEdgeRows, or on schema dbo.
-- GO starts a new batch, so a THROW before GO does not stop ALTER PROCEDURE.
-- A failed guard turns NOEXEC on. Later batches are compiled and not executed.
-- SET NOEXEC OFF still runs and restores the session.

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    RAISERROR('Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 16, 1);
    SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'dbo.pb_show_tree_step5_graph_expansion', N'P') IS NULL
   OR TYPE_ID(N'dbo.pb_WalkTopologyRows') IS NULL
   OR TYPE_ID(N'dbo.pb_GraphEdgeRows') IS NULL
BEGIN
    RAISERROR('Stop: install 013 and the expansion procedure before this script.', 16, 1);
    SET NOEXEC ON;
END
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[pb_show_tree_step5_graph_expansion]
    @AccountId UNIQUEIDENTIFIER = NULL,
    @InputContractVersion int,
    @WalkTopology dbo.pb_WalkTopologyRows READONLY,
    @TopologyEdges dbo.pb_GraphEdgeRows READONLY,
    @ExistingGraphEdges dbo.pb_GraphEdgeRows READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @InputContractVersion IS NULL OR @InputContractVersion <> 1
    BEGIN
        RAISERROR('@InputContractVersion must be 1.', 16, 1);
        RETURN;
    END;

    DECLARE @SrcWalkTopology TABLE
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ObjectType NVARCHAR(32) NOT NULL,
        ObjectId UNIQUEIDENTIFIER NOT NULL,
        Depth INT NOT NULL,
        PathKey NVARCHAR(MAX) NOT NULL
    );

    DECLARE @SrcTopologyEdges TABLE
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ChildObjectType NVARCHAR(32) NOT NULL,
        ChildObjectId UNIQUEIDENTIFIER NOT NULL,
        EdgeRank INT NOT NULL,
        EdgeSource NVARCHAR(64) NOT NULL
    );

    INSERT INTO @SrcWalkTopology (ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey)
    SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
    FROM @WalkTopology;

    INSERT INTO @SrcTopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
    FROM @TopologyEdges;

    CREATE TABLE #BuiltGraphEdges
    (
        ParentObjectType nvarchar(32) NOT NULL,
        ParentObjectId uniqueidentifier NOT NULL,
        ChildObjectType nvarchar(32) NOT NULL,
        ChildObjectId uniqueidentifier NOT NULL,
        EdgeRank int NOT NULL,
        EdgeSource nvarchar(64) NOT NULL
    );

    INSERT INTO #BuiltGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
    FROM @ExistingGraphEdges;

    SELECT DISTINCT wt.ObjectId AS SystemId
    INTO #ExpansionScopedSystems
    FROM @SrcWalkTopology wt
    WHERE wt.ObjectType = N'system';

    SELECT
        g.SystemId,
        g.GroupId,
        g.GroupName,
        COALESCE(g.ParentGroupId, gp.GroupId) AS ResolvedParentGroupId
    INTO #ExpansionGroupResolved
    FROM dbo.pb_groups g
    JOIN #ExpansionScopedSystems ss
      ON ss.SystemId = g.SystemId
    LEFT JOIN dbo.pb_groups gp
      ON gp.SystemId = g.SystemId
     AND gp.SourceGroupId = g.ParentSourceGroupId;

    INSERT INTO #BuiltGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT DISTINCT
        te.ParentObjectType,
        te.ParentObjectId,
        te.ChildObjectType,
        te.ChildObjectId,
        te.EdgeRank,
        te.EdgeSource
    FROM @SrcTopologyEdges te;

    INSERT INTO #BuiltGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT
        N'system',
        gr.SystemId,
        N'group',
        gr.GroupId,
        2000 + ROW_NUMBER() OVER (PARTITION BY gr.SystemId ORDER BY gr.GroupName, gr.GroupId),
        N'system-group'
    FROM #ExpansionGroupResolved gr
    WHERE (gr.ResolvedParentGroupId IS NULL
       OR  NOT EXISTS (SELECT 1 FROM #ExpansionGroupResolved p WHERE p.GroupId = gr.ResolvedParentGroupId))
      AND NOT EXISTS
      (
          SELECT 1
          FROM #BuiltGraphEdges e
          WHERE e.ParentObjectType = N'system'
            AND e.ParentObjectId = gr.SystemId
            AND e.ChildObjectType = N'group'
            AND e.ChildObjectId = gr.GroupId
      );

    INSERT INTO #BuiltGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT
        N'group',
        gr.ResolvedParentGroupId,
        N'group',
        gr.GroupId,
        2000 + ROW_NUMBER() OVER (PARTITION BY gr.ResolvedParentGroupId ORDER BY gr.GroupName, gr.GroupId),
        N'group-group'
    FROM #ExpansionGroupResolved gr
    WHERE gr.ResolvedParentGroupId IS NOT NULL
      AND EXISTS (SELECT 1 FROM #ExpansionGroupResolved p WHERE p.GroupId = gr.ResolvedParentGroupId)
      AND NOT EXISTS
      (
          SELECT 1
          FROM #BuiltGraphEdges e
          WHERE e.ParentObjectType = N'group'
            AND e.ParentObjectId = gr.ResolvedParentGroupId
            AND e.ChildObjectType = N'group'
            AND e.ChildObjectId = gr.GroupId
      );

    ;WITH GroupNodes AS
    (
        SELECT DISTINCT ge.ChildObjectId AS GroupId
        FROM #BuiltGraphEdges ge
        WHERE ge.ChildObjectType = N'group'
        UNION
        SELECT DISTINCT ge.ParentObjectId AS GroupId
        FROM #BuiltGraphEdges ge
        WHERE ge.ParentObjectType = N'group'
    )
    INSERT INTO #BuiltGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT
        N'group',
        gn.GroupId,
        N'member',
        m.MemberId,
        1000 + ROW_NUMBER() OVER (PARTITION BY gn.GroupId ORDER BY m.DisplayName, m.MemberId),
        N'group-membership'
    FROM GroupNodes gn
    JOIN dbo.pb_group_members gm
      ON gm.GroupId = gn.GroupId
    JOIN dbo.pb_members m
      ON m.MemberId = gm.MemberId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM #BuiltGraphEdges e
        WHERE e.ParentObjectType = N'group'
          AND e.ParentObjectId = gn.GroupId
          AND e.ChildObjectType = N'member'
          AND e.ChildObjectId = m.MemberId
    );

    INSERT INTO #BuiltGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT
        N'system',
        s.ObjectId,
        N'member',
        m.MemberId,
        ROW_NUMBER() OVER (PARTITION BY s.ObjectId ORDER BY m.DisplayName, m.MemberId),
        N'system-member'
    FROM (SELECT DISTINCT wt.ObjectId FROM @SrcWalkTopology wt WHERE wt.ObjectType = N'system') s
    JOIN dbo.pb_members m
      ON m.SystemId = s.ObjectId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM #BuiltGraphEdges e
        WHERE e.ParentObjectType = N'system'
          AND e.ParentObjectId = s.ObjectId
          AND e.ChildObjectType = N'member'
          AND e.ChildObjectId = m.MemberId
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM #BuiltGraphEdges pe
        JOIN dbo.pb_place_members pm
          ON pm.PlaceId = pe.ChildObjectId
         AND pm.MemberId = m.MemberId
        WHERE pe.ParentObjectType = N'system'
          AND pe.ParentObjectId = s.ObjectId
          AND pe.ChildObjectType = N'place'
    );

    INSERT INTO #BuiltGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT
        N'place',
        p.ObjectId,
        N'member',
        m.MemberId,
        1000 + ROW_NUMBER() OVER (PARTITION BY p.ObjectId ORDER BY ISNULL(pm.MembershipRank, 999999), m.DisplayName, m.MemberId),
        N'place-membership'
    FROM
    (
        SELECT DISTINCT wt.ObjectId
        FROM @SrcWalkTopology wt
        WHERE wt.ObjectType = N'place'
        UNION
        SELECT DISTINCT ge.ChildObjectId
        FROM #BuiltGraphEdges ge
        WHERE ge.ChildObjectType = N'place'
    ) p
    JOIN dbo.pb_place_members pm
      ON pm.PlaceId = p.ObjectId
    JOIN dbo.pb_members m
      ON m.MemberId = pm.MemberId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM #BuiltGraphEdges e
        WHERE e.ParentObjectType = N'place'
          AND e.ParentObjectId = p.ObjectId
          AND e.ChildObjectType = N'member'
          AND e.ChildObjectId = m.MemberId
    );

    SELECT
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    FROM #BuiltGraphEdges;
END;
GO


SET NOEXEC OFF;
GO