-- Validate the selected branch on the graph this procedure already built.
-- This script has not been executed.
-- Install it before rebuilding the API that passes @BranchJson.
-- It does not create tables and does not rerun scripts 001 through 006.
-- The first result set stays the nine profile columns.
-- @BranchJson NULL keeps that single result set, so a caller that does
-- not pass the parameter behaves as it does today.
-- A supplied @BranchJson adds two result sets from #GraphEdges before
-- this procedure returns: BranchValid, then the anchor rows in that
-- branch. It does not build the plural map a second time.
-- GO starts a new batch, so a THROW before GO does not stop ALTER
-- PROCEDURE. A wrong database turns NOEXEC on first. Later batches,
-- including ALTER PROCEDURE, are then compiled and not executed.
-- SET NOEXEC OFF is honored while NOEXEC is on, so the session is
-- restored at the end.

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    RAISERROR('Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 16, 1);
    SET NOEXEC ON;
END
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[pb_get_anchor_member_profiles_by_email]
    @Email nvarchar(320),
    @AnchorObjectType nvarchar(64),
    @AnchorName nvarchar(510),
    @BranchJson nvarchar(max) = NULL
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
        m.Description,
        m.Color,
        vt.VisibilityTypeId,
        vt.VisibilityCode AS VisibilityTypeCode,
        vt.DisplayName AS VisibilityTypeName,
        CAST(CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM dbo.pb_front_history AS fh
                WHERE fh.MemberId = m.MemberId
                  AND fh.SystemId = m.SystemId
                  AND fh.EndTimeMs IS NULL
                  AND fh.IsLive = 1
            )
            THEN 1
            ELSE 0
        END AS bit) AS IsFronting
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
    LEFT JOIN dbo.pb_visibility_types AS vt
        ON vt.VisibilityTypeId = m.VisibilityTypeId
    ORDER BY
        anchorMembers.EdgeRank,
        m.DisplayName,
        m.MemberId;

    IF @BranchJson IS NOT NULL
    BEGIN
        DECLARE @BranchNodes TABLE
        (
            Ordinal int NOT NULL PRIMARY KEY,
            ObjectType nvarchar(32) NOT NULL,
            ObjectId uniqueidentifier NOT NULL
        );

        DECLARE @SubmittedCount int = 0;
        DECLARE @BranchCount int = 0;
        DECLARE @BranchValid bit = 0;

        IF ISJSON(@BranchJson) = 1
           AND LEFT(LTRIM(@BranchJson), 1) = N'['
        BEGIN
            SELECT @SubmittedCount = COUNT(*)
            FROM OPENJSON(@BranchJson);

            INSERT INTO @BranchNodes (Ordinal, ObjectType, ObjectId)
            SELECT
                TRY_CONVERT(int, item.[key]),
                fields.ObjectType,
                fields.ObjectId
            FROM OPENJSON(@BranchJson) AS item
            CROSS APPLY OPENJSON(item.[value])
            WITH
            (
                ObjectType nvarchar(32) '$.objectType',
                ObjectId uniqueidentifier '$.objectId'
            ) AS fields
            WHERE TRY_CONVERT(int, item.[key]) IS NOT NULL
              AND fields.ObjectType IS NOT NULL
              AND fields.ObjectId IS NOT NULL;

            SELECT @BranchCount = COUNT(*)
            FROM @BranchNodes;
        END

        IF @SubmittedCount = @BranchCount
           AND @BranchCount BETWEEN 1 AND 32
           AND NOT EXISTS
           (
               SELECT ObjectType, ObjectId
               FROM @BranchNodes
               GROUP BY ObjectType, ObjectId
               HAVING COUNT(*) > 1
           )
           AND NOT EXISTS
           (
               SELECT 1
               FROM @BranchNodes
               WHERE ObjectType COLLATE Latin1_General_100_BIN2
                     NOT IN (N'system', N'group', N'place')
           )
           AND EXISTS
           (
               SELECT 1
               FROM @BranchNodes
               WHERE Ordinal = 0
                 AND ObjectType COLLATE Latin1_General_100_BIN2
                     = @AnchorObjectType COLLATE Latin1_General_100_BIN2
                 AND ObjectId = @AnchorObjectId
           )
           AND (SELECT MIN(Ordinal) FROM @BranchNodes) = 0
           AND (SELECT MAX(Ordinal) FROM @BranchNodes) = @BranchCount - 1
           AND NOT EXISTS
           (
               SELECT 1
               FROM @BranchNodes AS child
               INNER JOIN @BranchNodes AS parent
                   ON parent.Ordinal = child.Ordinal + 1
               WHERE NOT EXISTS
               (
                   SELECT 1
                   FROM #GraphEdges AS ge
                   WHERE ge.ParentObjectType COLLATE Latin1_General_100_BIN2
                         = parent.ObjectType COLLATE Latin1_General_100_BIN2
                     AND ge.ParentObjectId = parent.ObjectId
                     AND ge.ChildObjectType COLLATE Latin1_General_100_BIN2
                         = child.ObjectType COLLATE Latin1_General_100_BIN2
                     AND ge.ChildObjectId = child.ObjectId
               )
           )
           AND EXISTS
           (
               SELECT 1
               FROM @BranchNodes AS root
               INNER JOIN #GraphEdges AS ge
                   ON ge.ParentObjectType = N'account'
                  AND ge.ParentObjectId = @AccountId
                  AND ge.ChildObjectType COLLATE Latin1_General_100_BIN2
                      = root.ObjectType COLLATE Latin1_General_100_BIN2
                  AND ge.ChildObjectId = root.ObjectId
               WHERE root.Ordinal = @BranchCount - 1
           )
            SET @BranchValid = 1;

        SELECT @BranchValid AS BranchValid;

        IF @BranchValid = 1
           AND OBJECT_ID(N'dbo.pb_anchor_visibility_permissions', N'U') IS NOT NULL
        BEGIN
            SELECT
                node.Ordinal,
                node.ObjectType,
                node.ObjectId,
                setting.VisibilityMode,
                setting.VisibilityTypeId,
                setting.PermissionMode,
                setting.PermissionCode
            FROM @BranchNodes AS node
            LEFT JOIN dbo.pb_anchor_visibility_permissions AS setting
                ON setting.AccountId = @AccountId
               AND (
                    (node.ObjectType = N'system' AND setting.SystemId = node.ObjectId)
                    OR (node.ObjectType = N'group' AND setting.GroupId = node.ObjectId)
                    OR (node.ObjectType = N'place' AND setting.PlaceId = node.ObjectId)
               )
            ORDER BY node.Ordinal;
        END
        ELSE IF @BranchValid = 1
        BEGIN
            SELECT
                node.Ordinal,
                node.ObjectType,
                node.ObjectId,
                CAST(NULL AS nvarchar(16)) AS VisibilityMode,
                CAST(NULL AS int) AS VisibilityTypeId,
                CAST(NULL AS nvarchar(16)) AS PermissionMode,
                CAST(NULL AS nvarchar(16)) AS PermissionCode
            FROM @BranchNodes AS node
            ORDER BY node.Ordinal;
        END
        ELSE
        BEGIN
            SELECT
                CAST(NULL AS int) AS Ordinal,
                CAST(NULL AS nvarchar(32)) AS ObjectType,
                CAST(NULL AS uniqueidentifier) AS ObjectId,
                CAST(NULL AS nvarchar(16)) AS VisibilityMode,
                CAST(NULL AS int) AS VisibilityTypeId,
                CAST(NULL AS nvarchar(16)) AS PermissionMode,
                CAST(NULL AS nvarchar(16)) AS PermissionCode
            WHERE 1 = 0;
        END
    END
END;
GO

SET NOEXEC OFF;
GO
