/****** Object:  StoredProcedure [dbo].[pb_get_anchor_member_profiles_by_email]    Script Date: 11/9/2026 8:38:03 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[pb_get_anchor_member_profiles_by_email]
    @Email nvarchar(320),
    @AnchorObjectType nvarchar(64),
    @AnchorName nvarchar(510)
AS
BEGIN
    SET NOCOUNT ON;

    IF NULLIF(LTRIM(RTRIM(@Email)), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@AnchorObjectType)), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@AnchorName)), N'') IS NULL
        THROW 50001, 'Supply an email, anchor object type, and anchor name.', 1;

    IF @AnchorObjectType COLLATE Latin1_General_100_BIN2
       NOT IN (N'system', N'group', N'place')
        THROW 50002, 'Anchor object type must be system, group, or place.', 1;

    DECLARE @AccountId uniqueidentifier;
    DECLARE @AccountCount int;

    SELECT @AccountCount = COUNT(*)
    FROM dbo.pb_accounts
    WHERE Email = @Email;

    IF @AccountCount = 0
        THROW 50003, 'The account was not found.', 1;

    IF @AccountCount > 1
        THROW 50003, 'More than one account matches that email.', 1;

    SELECT @AccountId = AccountId
    FROM dbo.pb_accounts
    WHERE Email = @Email;

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

    EXEC dbo.pb_show_tree_step5_topology_seed_builder
        @AccountId = @AccountId;

    EXEC dbo.pb_show_tree_step5_topology_walker
        @AccountId = @AccountId;

    EXEC dbo.pb_show_tree_step5_graph_expansion
        @AccountId = @AccountId;

    DECLARE @AccountMapAnchors TABLE
    (
        AccountId uniqueidentifier NOT NULL,
        ObjectType nvarchar(32) NOT NULL,
        ObjectId uniqueidentifier NOT NULL,
        PRIMARY KEY (AccountId, ObjectType, ObjectId)
    );

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
        INNER JOIN #GraphEdges AS e
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
              WHEN N'group' THEN gr.GroupName
              WHEN N'place' THEN pl.PlaceName
          END = @AnchorName;

    DECLARE @AnchorCount int;
    DECLARE @AnchorObjectId uniqueidentifier;

    SELECT @AnchorCount = COUNT(*)
    FROM @MatchingAnchors;

    IF @AnchorCount = 0
        THROW 50004, 'No anchor with that type and name exists in this account map.', 1;

    IF @AnchorCount > 1
        THROW 50005, 'Multiple anchors in this account map have that type and name; the name is ambiguous.', 1;

    SELECT @AnchorObjectId = ObjectId
    FROM @MatchingAnchors;

    DECLARE @MembershipCount int;
    DECLARE @ActiveMembershipCount int;

    SELECT
        @MembershipCount = COUNT(*),
        @ActiveMembershipCount = COALESCE(
            SUM(
                CASE
                    WHEN s.StatusName = N'Active'
                     AND s.IsActive = 1
                    THEN 1
                    ELSE 0
                END
            ),
            0
        )
    FROM dbo.pb_object_memberships AS om
    LEFT JOIN dbo.pb_system_membership_statuses AS s
        ON s.MembershipStatusId = om.MembershipStatusId
    WHERE om.AccountId = @AccountId
      AND om.ObjectType COLLATE Latin1_General_100_BIN2
          = @AnchorObjectType COLLATE Latin1_General_100_BIN2
      AND TRY_CONVERT(uniqueidentifier, om.ObjectId) = @AnchorObjectId;

    IF @MembershipCount = 0
        THROW 50006, 'No access membership exists for this account on the requested anchor.', 1;

    IF @MembershipCount > 1
        THROW 50007, 'More than one access membership matches this account and anchor.', 1;

    IF @ActiveMembershipCount <> 1
        THROW 50008, 'The access membership for this account on the requested anchor is not active or its status could not be resolved.', 1;

    SELECT
        m.MemberId,
        m.DisplayName,
        m.Pronouns,
        m.Description
    FROM dbo.pb_members AS m
    INNER JOIN
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
END;
GO


