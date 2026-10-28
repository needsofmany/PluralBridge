/****** Object:  StoredProcedure [dbo].[pb_show_tree_step5_graph_expansion]    Script Date: 10/27/2026 7:05:43 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


ALTER   PROCEDURE [dbo].[pb_show_tree_step5_graph_expansion]
    @AccountId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF OBJECT_ID('tempdb..#WalkTopology') IS NULL AND OBJECT_ID('tempdb..##WalkTopology') IS NULL
    BEGIN
        RAISERROR('#WalkTopology or ##WalkTopology must exist before graph expansion.', 16, 1);
        RETURN;
    END;

    IF OBJECT_ID('tempdb..#TopologyEdges') IS NULL AND OBJECT_ID('tempdb..##TopologyEdges') IS NULL
    BEGIN
        RAISERROR('#TopologyEdges or ##TopologyEdges must exist before graph expansion.', 16, 1);
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

    IF OBJECT_ID('tempdb..#WalkTopology') IS NOT NULL
    BEGIN
        INSERT INTO @SrcWalkTopology (ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey)
        SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
        FROM #WalkTopology;
    END
    ELSE
    BEGIN
        INSERT INTO @SrcWalkTopology (ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey)
        SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
        FROM ##WalkTopology;
    END;

    IF OBJECT_ID('tempdb..#TopologyEdges') IS NOT NULL
    BEGIN
        INSERT INTO @SrcTopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
        SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
        FROM #TopologyEdges;
    END
    ELSE
    BEGIN
        INSERT INTO @SrcTopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
        SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
        FROM ##TopologyEdges;
    END;

    IF OBJECT_ID('tempdb..##GraphEdges') IS NOT NULL DROP TABLE ##GraphEdges;

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

    SELECT DISTINCT
        te.ParentObjectType,
        te.ParentObjectId,
        te.ChildObjectType,
        te.ChildObjectId,
        te.EdgeRank,
        te.EdgeSource
    INTO ##GraphEdges
    FROM @SrcTopologyEdges te;

    INSERT INTO ##GraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
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
          FROM ##GraphEdges e
          WHERE e.ParentObjectType = N'system'
            AND e.ParentObjectId = gr.SystemId
            AND e.ChildObjectType = N'group'
            AND e.ChildObjectId = gr.GroupId
      );

    INSERT INTO ##GraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
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
          FROM ##GraphEdges e
          WHERE e.ParentObjectType = N'group'
            AND e.ParentObjectId = gr.ResolvedParentGroupId
            AND e.ChildObjectType = N'group'
            AND e.ChildObjectId = gr.GroupId
      );

    ;WITH GroupNodes AS
    (
        SELECT DISTINCT ge.ChildObjectId AS GroupId
        FROM ##GraphEdges ge
        WHERE ge.ChildObjectType = N'group'
        UNION
        SELECT DISTINCT ge.ParentObjectId AS GroupId
        FROM ##GraphEdges ge
        WHERE ge.ParentObjectType = N'group'
    )
    INSERT INTO ##GraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
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
        FROM ##GraphEdges e
        WHERE e.ParentObjectType = N'group'
          AND e.ParentObjectId = gn.GroupId
          AND e.ChildObjectType = N'member'
          AND e.ChildObjectId = m.MemberId
    );

    INSERT INTO ##GraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
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
        FROM ##GraphEdges e
        WHERE e.ParentObjectType = N'system'
          AND e.ParentObjectId = s.ObjectId
          AND e.ChildObjectType = N'member'
          AND e.ChildObjectId = m.MemberId
    )
    AND NOT EXISTS
    (
        SELECT 1
        FROM ##GraphEdges pe
        JOIN dbo.pb_place_members pm
          ON pm.PlaceId = pe.ChildObjectId
         AND pm.MemberId = m.MemberId
        WHERE pe.ParentObjectType = N'system'
          AND pe.ParentObjectId = s.ObjectId
          AND pe.ChildObjectType = N'place'
    );

    INSERT INTO ##GraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
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
        FROM ##GraphEdges ge
        WHERE ge.ChildObjectType = N'place'
    ) p
    JOIN dbo.pb_place_members pm
      ON pm.PlaceId = p.ObjectId
    JOIN dbo.pb_members m
      ON m.MemberId = pm.MemberId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM ##GraphEdges e
        WHERE e.ParentObjectType = N'place'
          AND e.ParentObjectId = p.ObjectId
          AND e.ChildObjectType = N'member'
          AND e.ChildObjectId = m.MemberId
    );
END

GO


