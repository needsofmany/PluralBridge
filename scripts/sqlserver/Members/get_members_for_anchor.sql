USE [PluralBridgeChap2SafeSpine];
SET NOCOUNT ON;

/* Change only these three values. */
DECLARE @Email nvarchar(320) = N'example-6@pluralbridge.org';
DECLARE @AnchorObjectType nvarchar(64) = N'system';
DECLARE @AnchorName nvarchar(510) = N'The Harbor';

/* Validate inputs and resolve the account. */
IF NULLIF(LTRIM(RTRIM(@Email)), N'') IS NULL
   OR NULLIF(LTRIM(RTRIM(@AnchorObjectType)), N'') IS NULL
   OR NULLIF(LTRIM(RTRIM(@AnchorName)), N'') IS NULL
    THROW 50001, 'Enter the email, anchor type, and anchor name.', 1;

IF @AnchorObjectType COLLATE Latin1_General_100_BIN2
    NOT IN (N'system', N'group', N'place')
    THROW 50002, 'Anchor type must be system, group, or place.', 1;

IF (SELECT COUNT(*) FROM dbo.pb_accounts WHERE Email = @Email) <> 1
    THROW 50003, 'The email must identify exactly one account.', 1;

DECLARE @AccountId uniqueidentifier;

SELECT @AccountId = AccountId
FROM dbo.pb_accounts
WHERE Email = @Email;

/* Prepare session-local tables for the verified map pipeline. */
IF OBJECT_ID(N'tempdb..#TopologyEdges') IS NOT NULL
    DROP TABLE #TopologyEdges;

IF OBJECT_ID(N'tempdb..#SystemEdgeLabels') IS NOT NULL
    DROP TABLE #SystemEdgeLabels;

IF OBJECT_ID(N'tempdb..#WalkTopology') IS NOT NULL
    DROP TABLE #WalkTopology;

IF OBJECT_ID(N'tempdb..#GraphEdges') IS NOT NULL
    DROP TABLE #GraphEdges;

CREATE TABLE #TopologyEdges
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ChildObjectType nvarchar(32) NOT NULL,
    ChildObjectId uniqueidentifier NOT NULL,
    EdgeRank int NOT NULL,
    EdgeSource nvarchar(64) NOT NULL
);

CREATE TABLE #SystemEdgeLabels
(
    SystemId uniqueidentifier NOT NULL PRIMARY KEY,
    EdgeLabel nvarchar(max) NULL
);

CREATE TABLE #WalkTopology
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ObjectType nvarchar(32) NOT NULL,
    ObjectId uniqueidentifier NOT NULL,
    Depth int NOT NULL,
    PathKey nvarchar(max) NOT NULL
);

CREATE TABLE #GraphEdges
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ChildObjectType nvarchar(32) NOT NULL,
    ChildObjectId uniqueidentifier NOT NULL,
    EdgeRank int NOT NULL,
    EdgeSource nvarchar(64) NOT NULL
);

BEGIN TRY
    /* Build the map specifically for the resolved account. */
    DECLARE @SeedRows TABLE
    (
        RowKind nvarchar(8) NOT NULL,
        ParentObjectType nvarchar(32) NULL,
        ParentObjectId uniqueidentifier NULL,
        ChildObjectType nvarchar(32) NULL,
        ChildObjectId uniqueidentifier NULL,
        EdgeRank int NULL,
        EdgeSource nvarchar(64) NULL,
        SystemId uniqueidentifier NULL,
        EdgeLabel nvarchar(max) NULL
    );

    DELETE FROM @SeedRows;

    INSERT INTO @SeedRows
    (
        RowKind,
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource,
        SystemId,
        EdgeLabel
    )
    EXEC dbo.pb_show_tree_step5_topology_seed_builder
        @AccountId = @AccountId;

    INSERT INTO #TopologyEdges
    (
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    )
    SELECT
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    FROM @SeedRows
    WHERE RowKind = N'edge';

    INSERT INTO #SystemEdgeLabels
    (
        SystemId,
        EdgeLabel
    )
    SELECT
        SystemId,
        EdgeLabel
    FROM @SeedRows
    WHERE RowKind = N'label';

    EXEC dbo.pb_show_tree_step5_topology_walker
        @AccountId = @AccountId;

    DECLARE @WalkTopology dbo.pb_WalkTopologyRows;
    DECLARE @TopologyEdges dbo.pb_GraphEdgeRows;
    DECLARE @ExistingGraphEdges dbo.pb_GraphEdgeRows;
    DECLARE @ExpansionRows TABLE
    (
        ParentObjectType nvarchar(32) NOT NULL,
        ParentObjectId uniqueidentifier NOT NULL,
        ChildObjectType nvarchar(32) NOT NULL,
        ChildObjectId uniqueidentifier NOT NULL,
        EdgeRank int NOT NULL,
        EdgeSource nvarchar(64) NOT NULL
    );

    DELETE FROM @WalkTopology;
    DELETE FROM @TopologyEdges;
    DELETE FROM @ExistingGraphEdges;
    DELETE FROM @ExpansionRows;

    INSERT INTO @WalkTopology (ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey)
    SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
    FROM #WalkTopology;

    INSERT INTO @TopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
    FROM #TopologyEdges;

    INSERT INTO @ExistingGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
    FROM #GraphEdges;

    INSERT INTO @ExpansionRows
    (
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    )
    EXEC dbo.pb_show_tree_step5_graph_expansion
        @AccountId = @AccountId,
        @InputContractVersion = 1,
        @WalkTopology = @WalkTopology,
        @TopologyEdges = @TopologyEdges,
        @ExistingGraphEdges = @ExistingGraphEdges;

    DELETE FROM #GraphEdges;

    INSERT INTO #GraphEdges
    (
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    )
    SELECT
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    FROM @ExpansionRows;

    DECLARE @AccountMapAnchors TABLE
    (
        AccountId uniqueidentifier NOT NULL,
        ObjectType nvarchar(32) NOT NULL,
        ObjectId uniqueidentifier NOT NULL,
        PRIMARY KEY (AccountId, ObjectType, ObjectId)
    );

    /* Collect anchors reachable from THIS account's map root. */
    ;WITH MapWalk AS
    (
        SELECT
            e.ChildObjectType AS ObjectType,
            e.ChildObjectId AS ObjectId,
            CAST(
                N'|' + e.ChildObjectType + N':'
                + CONVERT(nvarchar(36), e.ChildObjectId) + N'|'
                AS nvarchar(max)
            ) AS Visited
        FROM #GraphEdges AS e
        WHERE e.ParentObjectType = N'account'
          AND e.ParentObjectId = @AccountId

        UNION ALL

        SELECT
            e.ChildObjectType,
            e.ChildObjectId,
            CAST(
                w.Visited + e.ChildObjectType + N':'
                + CONVERT(nvarchar(36), e.ChildObjectId) + N'|'
                AS nvarchar(max)
            )
        FROM MapWalk AS w
        JOIN #GraphEdges AS e
          ON e.ParentObjectType = w.ObjectType
         AND e.ParentObjectId = w.ObjectId
        WHERE w.Visited NOT LIKE
            N'%|' + e.ChildObjectType + N':'
            + CONVERT(nvarchar(36), e.ChildObjectId) + N'|%'
    )
    INSERT INTO @AccountMapAnchors
        (AccountId, ObjectType, ObjectId)
    SELECT DISTINCT
        @AccountId,
        ObjectType,
        ObjectId
    FROM MapWalk
    WHERE ObjectType IN (N'system', N'group', N'place')
    OPTION (MAXRECURSION 200);

    DECLARE @MatchingAnchors TABLE
    (
        ObjectId uniqueidentifier NOT NULL PRIMARY KEY
    );

    /* The account/map qualification excludes another map's Harbor. */
    INSERT INTO @MatchingAnchors (ObjectId)
    SELECT ma.ObjectId
    FROM @AccountMapAnchors AS ma
    LEFT JOIN dbo.pb_systems AS sy
      ON ma.ObjectType = N'system'
     AND sy.SystemId = ma.ObjectId
    LEFT JOIN dbo.pb_groups AS gr
      ON ma.ObjectType = N'group'
     AND gr.GroupId = ma.ObjectId
    LEFT JOIN dbo.pb_places AS pl
      ON ma.ObjectType = N'place'
     AND pl.PlaceId = ma.ObjectId
    WHERE ma.AccountId = @AccountId
      AND ma.ObjectType COLLATE Latin1_General_100_BIN2
          = @AnchorObjectType COLLATE Latin1_General_100_BIN2
      AND CASE ma.ObjectType
              WHEN N'system' THEN sy.SystemName
              WHEN N'group'  THEN gr.GroupName
              WHEN N'place'  THEN pl.PlaceName
          END = @AnchorName;

    IF NOT EXISTS (SELECT 1 FROM @MatchingAnchors)
        THROW 50004, 'No anchor with that type and name exists in this account''s map.', 1;

    IF (SELECT COUNT(*) FROM @MatchingAnchors) > 1
        THROW 50005, 'Multiple anchors in this account''s map have that type and name.', 1;

    DECLARE @AnchorObjectId uniqueidentifier;

    SELECT @AnchorObjectId = ObjectId
    FROM @MatchingAnchors;

    /* Independently verify access to the selected mapped anchor. */
    DECLARE @AccessCount int;
    DECLARE @ActiveAccessCount int;

    SELECT
        @AccessCount = COUNT(*),
        @ActiveAccessCount = COALESCE(SUM(
            CASE
                WHEN st.StatusName = N'Active'
                 AND st.IsActive = 1 THEN 1
                ELSE 0
            END
        ), 0)
    FROM dbo.pb_object_memberships AS om
    LEFT JOIN dbo.pb_system_membership_statuses AS st
      ON st.MembershipStatusId = om.MembershipStatusId
    WHERE om.AccountId = @AccountId
      AND om.ObjectType COLLATE Latin1_General_100_BIN2
          = @AnchorObjectType COLLATE Latin1_General_100_BIN2
      AND TRY_CONVERT(uniqueidentifier, om.ObjectId) = @AnchorObjectId;

    IF @AccessCount = 0
        THROW 50006, 'No access membership exists for this account and anchor.', 1;

    IF @AccessCount > 1
        THROW 50007, 'Multiple access memberships match this account and anchor.', 1;

    IF @ActiveAccessCount <> 1
        THROW 50008, 'The access membership is inactive or its status cannot be resolved.', 1;

    /* Return the selected anchor's direct members in map rank order. */
    SELECT
        m.MemberId,
        m.DisplayName,
        m.Pronouns,
        m.Description
    FROM dbo.pb_members AS m
    JOIN
    (
        SELECT
            ge.ChildObjectId AS MemberId,
            MIN(ge.EdgeRank) AS EdgeRank
        FROM #GraphEdges AS ge
        WHERE ge.ParentObjectType COLLATE Latin1_General_100_BIN2
              = @AnchorObjectType COLLATE Latin1_General_100_BIN2
          AND ge.ParentObjectId = @AnchorObjectId
          AND ge.ChildObjectType = N'member'
        GROUP BY ge.ChildObjectId
    ) AS anchorMembers
      ON anchorMembers.MemberId = m.MemberId
    ORDER BY
        anchorMembers.EdgeRank,
        m.DisplayName,
        m.MemberId;

    DROP TABLE #GraphEdges;
    DROP TABLE #WalkTopology;
    DROP TABLE #SystemEdgeLabels;
    DROP TABLE #TopologyEdges;
END TRY
BEGIN CATCH
    IF OBJECT_ID(N'tempdb..#GraphEdges') IS NOT NULL
        DROP TABLE #GraphEdges;

    IF OBJECT_ID(N'tempdb..#WalkTopology') IS NOT NULL
        DROP TABLE #WalkTopology;

    IF OBJECT_ID(N'tempdb..#SystemEdgeLabels') IS NOT NULL
        DROP TABLE #SystemEdgeLabels;

    IF OBJECT_ID(N'tempdb..#TopologyEdges') IS NOT NULL
        DROP TABLE #TopologyEdges;

    THROW;
END CATCH;