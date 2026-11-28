-- Canonical definition of dbo.pb_show_tree_step5_graph_expansion.
-- Deploy from scripts/sqlserver/Members/014_graph_expansion_result_set.sql
-- after 013_graph_edge_table_types.sql.
-- This procedure does not read or write caller temporary tables.
-- It returns one six-column result set. The caller replaces #GraphEdges
-- only after that result has been captured.
-- @InputContractVersion has no default. The accepted value is 1.
-- An empty walk, empty topology edges, or empty existing edges is a
-- legitimate input. Table-valued parameters cannot be omitted in a call;
-- pass an empty table variable for an empty input.

USE [PluralBridgeChap2SafeSpine];
GO

SET ANSI_NULLS ON;
GO

SET QUOTED_IDENTIFIER ON;
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
