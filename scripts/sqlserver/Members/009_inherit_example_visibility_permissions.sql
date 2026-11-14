-- Initialize missing Visibility and Permissions settings to inherit
-- for Examples 1-9, 11, and 12.
--
-- Scope
-- Target accounts are the pb_accounts rows whose Email or NormalizedEmail
-- is exactly example-1@pluralbridge.org through example-9@pluralbridge.org,
-- example-11@pluralbridge.org, or example-12@pluralbridge.org.
-- Their AccountIds are resolved from those rows. The number in the email
-- is not an AccountId. All 11 rows are required.
-- Example 10 is example-10@pluralbridge.org and must resolve to
-- AccountId 044B1420-0DC8-4EFA-8BDC-8BFC6D5A2FA5. It is not a target.
-- Every other account is excluded. pb_groups.AccountId is not used.
-- Example 1's legacy placement values group-001, group-002, and group-003
-- are not GUIDs. The installed seed does not turn them into anchors.
-- This script does not repair or delete them.
--
-- Inventory
-- The installed procedures build each account's graph:
--   dbo.pb_show_tree_step5_topology_seed_builder
--   dbo.pb_show_tree_step5_topology_walker
--   dbo.pb_show_tree_step5_graph_expansion
-- They are not modified. They require these connection-scoped tables:
--   #TopologyEdges, #SystemEdgeLabels, #WalkTopology, #GraphEdges.
-- Those tables are created before the transaction. Each account run
-- truncates all four before the procedure calls, so one account's edges
-- cannot remain in the next account's graph.
-- Anchor settings keys are AccountId plus one of SystemId, GroupId, or
-- PlaceId. Member settings keys are MemberId, GroupMemberId, or
-- PlaceMemberId. Keys are deduplicated. The same member under two
-- different keys remains two rows.
--
-- Checks before any permanent insert
-- The target inventory must contain 38 anchors and 65 member-placement
-- keys. Referenced systems, groups, places, members, and membership rows
-- must exist. A group or place graph edge with no membership row aborts,
-- because the sharing check would be incomplete.
-- The same member-placement key must not be reached by two target
-- accounts, by Example 10, or by any excluded account. Anchor rows are
-- already separated by AccountId and are not treated as one shared row.
-- An existing target row must already be inherit with both value columns
-- NULL, or be absent. Any other stored value aborts. Nothing is updated.
-- Example 10's member whose DisplayName is A must have a reached settings
-- row with explicit visibility type 3 and explicit permission manager.
-- That row, every other Example 10 settings row, and every excluded
-- settings row must still match their pre-insert checksums.
--
-- Execution
-- Set the SSMS database dropdown to PluralBridgeChap2SafeSpine.
-- @Apply is 0 by default. The script performs the inserts, verifies them,
-- and rolls the transaction back. Set @Apply to 1 to commit after the
-- same verification. The result says whether the run rolled back or
-- committed. A failed check rolls back and returns the conflicting rows
-- before the error.
--
-- Rerun
-- After a committed run, the 38 and 65 keys already store inherit.
-- A later run keeps those rows, inserts nothing, and verifies the same
-- counts. A rolled-back run leaves the permanent tables unchanged, so
-- the next run is the first write. Roles, access memberships, placements,
-- profile fields, avatars, and Fronting are not modified. No schema change.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

-- 0 rolls back after verification. 1 commits after the same verification.
DECLARE @Apply bit = 0;

DECLARE @Example10AccountId uniqueidentifier = '044B1420-0DC8-4EFA-8BDC-8BFC6D5A2FA5';
DECLARE @ExpectedAnchors int = 38;
DECLARE @ExpectedPlacements int = 65;
DECLARE @ScriptTransaction bit = 0;
DECLARE @Outcome nvarchar(32) = N'not completed';

DECLARE @AccountId uniqueidentifier;
DECLARE @Email nvarchar(320);
DECLARE @Scope nvarchar(16);

DECLARE @AnchorCount int;
DECLARE @PlacementCount int;
DECLARE @OutsideAnchorCountBefore int;
DECLARE @OutsideAnchorChecksumBefore int;
DECLARE @OutsideAnchorCountAfter int;
DECLARE @OutsideAnchorChecksumAfter int;
DECLARE @OutsideMemberCountBefore int;
DECLARE @OutsideMemberChecksumBefore int;
DECLARE @OutsideMemberCountAfter int;
DECLARE @OutsideMemberChecksumAfter int;
DECLARE @Example10AnchorCountBefore int;
DECLARE @Example10AnchorChecksumBefore int;
DECLARE @Example10AnchorCountAfter int;
DECLARE @Example10AnchorChecksumAfter int;
DECLARE @Example10MemberCountBefore int;
DECLARE @Example10MemberChecksumBefore int;
DECLARE @Example10MemberCountAfter int;
DECLARE @Example10MemberChecksumAfter int;

DECLARE @ExpectedEmail TABLE
(
    Email nvarchar(320) NOT NULL PRIMARY KEY
);

DECLARE @TargetAccount TABLE
(
    AccountId uniqueidentifier NOT NULL PRIMARY KEY,
    Email nvarchar(320) NOT NULL UNIQUE,
    Processed bit NOT NULL
);

DECLARE @OtherAccount TABLE
(
    AccountId uniqueidentifier NOT NULL PRIMARY KEY,
    Email nvarchar(320) NOT NULL,
    Processed bit NOT NULL
);

DECLARE @Anchor TABLE
(
    AccountId uniqueidentifier NOT NULL,
    ObjectType nvarchar(16) NOT NULL,
    ObjectId uniqueidentifier NOT NULL,
    PRIMARY KEY (AccountId, ObjectType, ObjectId)
);

DECLARE @Placement TABLE
(
    AccountId uniqueidentifier NOT NULL,
    PlacementType nvarchar(32) NOT NULL,
    SettingsKeyType nvarchar(32) NOT NULL,
    SettingsKey uniqueidentifier NOT NULL,
    MemberId uniqueidentifier NOT NULL,
    AnchorId uniqueidentifier NOT NULL,
    PRIMARY KEY (AccountId, SettingsKeyType, SettingsKey)
);

DECLARE @OtherKey TABLE
(
    AccountId uniqueidentifier NOT NULL,
    SettingsKeyType nvarchar(32) NOT NULL,
    SettingsKey uniqueidentifier NOT NULL,
    MemberId uniqueidentifier NOT NULL,
    PRIMARY KEY (AccountId, SettingsKeyType, SettingsKey)
);

DECLARE @MemberA TABLE
(
    SettingsKeyType nvarchar(32) NOT NULL,
    SettingsKey uniqueidentifier NOT NULL,
    MemberId uniqueidentifier NOT NULL,
    VisibilityMode nvarchar(16) NOT NULL,
    VisibilityTypeId int NULL,
    PermissionMode nvarchar(16) NOT NULL,
    PermissionCode nvarchar(16) NULL,
    PRIMARY KEY (SettingsKeyType, SettingsKey)
);

DECLARE @Issue TABLE
(
    AccountId uniqueidentifier NULL,
    Email nvarchar(320) NULL,
    Issue nvarchar(80) NOT NULL,
    Detail nvarchar(500) NOT NULL
);

DECLARE @Report TABLE
(
    Category nvarchar(32) NOT NULL PRIMARY KEY,
    Expected int NOT NULL,
    ExistingInherit int NOT NULL,
    Inserted int NOT NULL,
    VerifiedInherit int NOT NULL
);

INSERT INTO @ExpectedEmail (Email)
VALUES
    (N'example-1@pluralbridge.org'),
    (N'example-2@pluralbridge.org'),
    (N'example-3@pluralbridge.org'),
    (N'example-4@pluralbridge.org'),
    (N'example-5@pluralbridge.org'),
    (N'example-6@pluralbridge.org'),
    (N'example-7@pluralbridge.org'),
    (N'example-8@pluralbridge.org'),
    (N'example-9@pluralbridge.org'),
    (N'example-11@pluralbridge.org'),
    (N'example-12@pluralbridge.org');

BEGIN TRY
    IF @Apply NOT IN (0, 1)
        THROW 50000, 'Stop: set @Apply to 0 or 1.', 1;

    IF @@TRANCOUNT <> 0
        THROW 50000, 'Stop: this script must not run inside an existing transaction.', 1;

    IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
        THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

    IF OBJECT_ID(N'dbo.pb_show_tree_step5_topology_seed_builder', N'P') IS NULL
        OR OBJECT_ID(N'dbo.pb_show_tree_step5_topology_walker', N'P') IS NULL
        OR OBJECT_ID(N'dbo.pb_show_tree_step5_graph_expansion', N'P') IS NULL
        THROW 50000, 'Stop: the installed seed, walker, or expansion procedure is missing.', 1;

    IF OBJECT_ID(N'dbo.pb_anchor_visibility_permissions', N'U') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'AccountId') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'SystemId') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'GroupId') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'PlaceId') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'VisibilityMode') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'VisibilityTypeId') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'PermissionMode') IS NULL
        OR COL_LENGTH(N'dbo.pb_anchor_visibility_permissions', N'PermissionCode') IS NULL
        THROW 50000, 'Stop: dbo.pb_anchor_visibility_permissions is missing or unreadable.', 1;

    IF OBJECT_ID(N'dbo.pb_member_visibility_permissions', N'U') IS NULL
        OR COL_LENGTH(N'dbo.pb_member_visibility_permissions', N'MemberId') IS NULL
        OR COL_LENGTH(N'dbo.pb_member_visibility_permissions', N'GroupMemberId') IS NULL
        OR COL_LENGTH(N'dbo.pb_member_visibility_permissions', N'PlaceMemberId') IS NULL
        OR COL_LENGTH(N'dbo.pb_member_visibility_permissions', N'VisibilityMode') IS NULL
        OR COL_LENGTH(N'dbo.pb_member_visibility_permissions', N'VisibilityTypeId') IS NULL
        OR COL_LENGTH(N'dbo.pb_member_visibility_permissions', N'PermissionMode') IS NULL
        OR COL_LENGTH(N'dbo.pb_member_visibility_permissions', N'PermissionCode') IS NULL
        THROW 50000, 'Stop: dbo.pb_member_visibility_permissions is missing or unreadable.', 1;

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT
        NULL,
        expected.Email,
        N'missing-target-account',
        N'No pb_accounts row has this Email or NormalizedEmail.'
    FROM @ExpectedEmail AS expected
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.pb_accounts AS account
        WHERE account.Email = expected.Email
           OR account.NormalizedEmail = expected.Email
    );

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT
        NULL,
        expected.Email,
        N'ambiguous-target-account',
        N'More than one pb_accounts row matches this example email.'
    FROM @ExpectedEmail AS expected
    WHERE
    (
        SELECT COUNT(*)
        FROM dbo.pb_accounts AS account
        WHERE account.Email = expected.Email
           OR account.NormalizedEmail = expected.Email
    ) > 1;

    IF EXISTS (SELECT 1 FROM @Issue)
    BEGIN
        SELECT AccountId, Email, Issue, Detail FROM @Issue ORDER BY Issue, Email;
        THROW 50000, 'Stop: the 11 target example accounts could not be resolved.', 1;
    END;

    INSERT INTO @TargetAccount (AccountId, Email, Processed)
    SELECT account.AccountId, expected.Email, 0
    FROM @ExpectedEmail AS expected
    INNER JOIN dbo.pb_accounts AS account
        ON account.Email = expected.Email
        OR account.NormalizedEmail = expected.Email;

    IF (SELECT COUNT(*) FROM @TargetAccount) <> 11
        THROW 50000, 'Stop: target resolution did not produce 11 accounts.', 1;

    IF EXISTS (SELECT 1 FROM @TargetAccount WHERE AccountId = @Example10AccountId)
        THROW 50000, 'Stop: a target email resolved to the Example 10 AccountId.', 1;

    IF
    (
        SELECT COUNT(*)
        FROM dbo.pb_accounts AS account
        WHERE account.AccountId = @Example10AccountId
          AND (account.Email = N'example-10@pluralbridge.org'
               OR account.NormalizedEmail = N'example-10@pluralbridge.org')
    ) <> 1
        THROW 50000, 'Stop: Example 10 email does not resolve to AccountId 044B1420-0DC8-4EFA-8BDC-8BFC6D5A2FA5.', 1;

    INSERT INTO @OtherAccount (AccountId, Email, Processed)
    SELECT account.AccountId, account.Email, 0
    FROM dbo.pb_accounts AS account
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM @TargetAccount AS target
        WHERE target.AccountId = account.AccountId
    );

    IF NOT EXISTS (SELECT 1 FROM @OtherAccount WHERE AccountId = @Example10AccountId)
        THROW 50000, 'Stop: Example 10 is not in the excluded-account graph check.', 1;

    IF OBJECT_ID(N'tempdb..#TopologyEdges') IS NOT NULL DROP TABLE #TopologyEdges;
    IF OBJECT_ID(N'tempdb..#SystemEdgeLabels') IS NOT NULL DROP TABLE #SystemEdgeLabels;
    IF OBJECT_ID(N'tempdb..#WalkTopology') IS NOT NULL DROP TABLE #WalkTopology;
    IF OBJECT_ID(N'tempdb..#GraphEdges') IS NOT NULL DROP TABLE #GraphEdges;
    IF OBJECT_ID(N'tempdb..#CurrentAnchor') IS NOT NULL DROP TABLE #CurrentAnchor;
    IF OBJECT_ID(N'tempdb..#CurrentPlacement') IS NOT NULL DROP TABLE #CurrentPlacement;

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

    CREATE TABLE #CurrentAnchor
    (
        ObjectType nvarchar(16) NOT NULL,
        ObjectId uniqueidentifier NOT NULL,
        PRIMARY KEY (ObjectType, ObjectId)
    );

    CREATE TABLE #CurrentPlacement
    (
        PlacementType nvarchar(32) NOT NULL,
        SettingsKeyType nvarchar(32) NOT NULL,
        SettingsKey uniqueidentifier NOT NULL,
        MemberId uniqueidentifier NOT NULL,
        AnchorId uniqueidentifier NOT NULL,
        PRIMARY KEY (SettingsKeyType, SettingsKey)
    );

    BEGIN TRANSACTION;
    SET @ScriptTransaction = 1;

    IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
        THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

    WHILE EXISTS (SELECT 1 FROM @TargetAccount WHERE Processed = 0)
       OR EXISTS (SELECT 1 FROM @OtherAccount WHERE Processed = 0)
    BEGIN
        SET @AccountId = NULL;
        SET @Email = NULL;
        SET @Scope = NULL;

        SELECT TOP (1)
            @AccountId = AccountId,
            @Email = Email,
            @Scope = N'target'
        FROM @TargetAccount
        WHERE Processed = 0
        ORDER BY Email;

        IF @AccountId IS NULL
        BEGIN
            SELECT TOP (1)
                @AccountId = AccountId,
                @Email = Email,
                @Scope = N'other'
            FROM @OtherAccount
            WHERE Processed = 0
            ORDER BY Email;
        END;

        TRUNCATE TABLE #TopologyEdges;
        TRUNCATE TABLE #SystemEdgeLabels;
        TRUNCATE TABLE #WalkTopology;
        TRUNCATE TABLE #GraphEdges;
        TRUNCATE TABLE #CurrentAnchor;
        TRUNCATE TABLE #CurrentPlacement;

        EXEC dbo.pb_show_tree_step5_topology_seed_builder
            @AccountId = @AccountId;

        EXEC dbo.pb_show_tree_step5_topology_walker
            @AccountId = @AccountId;

        EXEC dbo.pb_show_tree_step5_graph_expansion
            @AccountId = @AccountId;

        INSERT INTO #CurrentAnchor (ObjectType, ObjectId)
        SELECT DISTINCT edge.ObjectType, edge.ObjectId
        FROM
        (
            SELECT ChildObjectType AS ObjectType, ChildObjectId AS ObjectId
            FROM #GraphEdges
            WHERE ChildObjectType IN (N'system', N'group', N'place')
            UNION
            SELECT ParentObjectType, ParentObjectId
            FROM #GraphEdges
            WHERE ParentObjectType IN (N'system', N'group', N'place')
        ) AS edge;

        INSERT INTO @Issue (AccountId, Email, Issue, Detail)
        SELECT
            @AccountId,
            @Email,
            N'unresolved-group-membership',
            N'Group ' + CONVERT(nvarchar(36), edge.ParentObjectId)
                + N' has a group-membership edge to member '
                + CONVERT(nvarchar(36), edge.ChildObjectId)
                + N' and no pb_group_members row.'
        FROM #GraphEdges AS edge
        WHERE edge.EdgeSource = N'group-membership'
          AND edge.ParentObjectType = N'group'
          AND edge.ChildObjectType = N'member'
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.pb_group_members AS membership
              WHERE membership.GroupId = edge.ParentObjectId
                AND membership.MemberId = edge.ChildObjectId
          );

        INSERT INTO @Issue (AccountId, Email, Issue, Detail)
        SELECT
            @AccountId,
            @Email,
            N'unresolved-place-membership',
            N'Place ' + CONVERT(nvarchar(36), edge.ParentObjectId)
                + N' has a place-membership edge to member '
                + CONVERT(nvarchar(36), edge.ChildObjectId)
                + N' and no pb_place_members row.'
        FROM #GraphEdges AS edge
        WHERE edge.EdgeSource = N'place-membership'
          AND edge.ParentObjectType = N'place'
          AND edge.ChildObjectType = N'member'
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.pb_place_members AS membership
              WHERE membership.PlaceId = edge.ParentObjectId
                AND membership.MemberId = edge.ChildObjectId
          );

        INSERT INTO #CurrentPlacement
        (
            PlacementType, SettingsKeyType, SettingsKey, MemberId, AnchorId
        )
        SELECT
            N'system-member',
            N'MemberId',
            edge.ChildObjectId,
            edge.ChildObjectId,
            MIN(edge.ParentObjectId)
        FROM #GraphEdges AS edge
        WHERE edge.EdgeSource = N'system-member'
          AND edge.ParentObjectType = N'system'
          AND edge.ChildObjectType = N'member'
        GROUP BY edge.ChildObjectId;

        INSERT INTO #CurrentPlacement
        (
            PlacementType, SettingsKeyType, SettingsKey, MemberId, AnchorId
        )
        SELECT DISTINCT
            N'group-member',
            N'GroupMemberId',
            membership.GroupMemberId,
            membership.MemberId,
            edge.ParentObjectId
        FROM #GraphEdges AS edge
        INNER JOIN dbo.pb_group_members AS membership
            ON membership.GroupId = edge.ParentObjectId
           AND membership.MemberId = edge.ChildObjectId
        WHERE edge.EdgeSource = N'group-membership'
          AND edge.ParentObjectType = N'group'
          AND edge.ChildObjectType = N'member'
          AND NOT EXISTS
          (
              SELECT 1
              FROM #CurrentPlacement AS existing
              WHERE existing.SettingsKeyType = N'GroupMemberId'
                AND existing.SettingsKey = membership.GroupMemberId
          );

        INSERT INTO #CurrentPlacement
        (
            PlacementType, SettingsKeyType, SettingsKey, MemberId, AnchorId
        )
        SELECT DISTINCT
            N'place-member',
            N'PlaceMemberId',
            membership.PlaceMemberId,
            membership.MemberId,
            edge.ParentObjectId
        FROM #GraphEdges AS edge
        INNER JOIN dbo.pb_place_members AS membership
            ON membership.PlaceId = edge.ParentObjectId
           AND membership.MemberId = edge.ChildObjectId
        WHERE edge.EdgeSource = N'place-membership'
          AND edge.ParentObjectType = N'place'
          AND edge.ChildObjectType = N'member'
          AND NOT EXISTS
          (
              SELECT 1
              FROM #CurrentPlacement AS existing
              WHERE existing.SettingsKeyType = N'PlaceMemberId'
                AND existing.SettingsKey = membership.PlaceMemberId
          );

        IF @Scope = N'target'
        BEGIN
            INSERT INTO @Anchor (AccountId, ObjectType, ObjectId)
            SELECT @AccountId, ObjectType, ObjectId
            FROM #CurrentAnchor;

            INSERT INTO @Placement
            (
                AccountId, PlacementType, SettingsKeyType,
                SettingsKey, MemberId, AnchorId
            )
            SELECT
                @AccountId, PlacementType, SettingsKeyType,
                SettingsKey, MemberId, AnchorId
            FROM #CurrentPlacement;

            UPDATE @TargetAccount
            SET Processed = 1
            WHERE AccountId = @AccountId;
        END
        ELSE
        BEGIN
            INSERT INTO @OtherKey (AccountId, SettingsKeyType, SettingsKey, MemberId)
            SELECT @AccountId, SettingsKeyType, SettingsKey, MemberId
            FROM #CurrentPlacement;

            IF @AccountId = @Example10AccountId
            BEGIN
                INSERT INTO @MemberA
                (
                    SettingsKeyType, SettingsKey, MemberId,
                    VisibilityMode, VisibilityTypeId, PermissionMode, PermissionCode
                )
                SELECT
                    placement.SettingsKeyType,
                    placement.SettingsKey,
                    placement.MemberId,
                    setting.VisibilityMode,
                    setting.VisibilityTypeId,
                    setting.PermissionMode,
                    setting.PermissionCode
                FROM #CurrentPlacement AS placement
                INNER JOIN dbo.pb_members AS member
                    ON member.MemberId = placement.MemberId
                   AND member.DisplayName = N'A'
                INNER JOIN dbo.pb_member_visibility_permissions AS setting
                    ON (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
                    OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
                    OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
                WHERE setting.VisibilityMode = N'explicit'
                  AND setting.VisibilityTypeId = 3
                  AND setting.PermissionMode = N'explicit'
                  AND setting.PermissionCode = N'manager';
            END;

            UPDATE @OtherAccount
            SET Processed = 1
            WHERE AccountId = @AccountId;
        END;
    END;

    IF EXISTS (SELECT 1 FROM @Issue)
    BEGIN
        SELECT AccountId, Email, Issue, Detail FROM @Issue ORDER BY Issue, Email, Detail;
        THROW 50000, 'Stop: a graph edge could not be resolved to a membership row.', 1;
    END;

    SELECT @AnchorCount = COUNT(*) FROM @Anchor;
    SELECT @PlacementCount = COUNT(*)
    FROM
    (
        SELECT DISTINCT SettingsKeyType, SettingsKey
        FROM @Placement
    ) AS distinctKey;

    IF @AnchorCount <> @ExpectedAnchors OR @PlacementCount <> @ExpectedPlacements
    BEGIN
        SELECT
            @AnchorCount AS AnchorCount,
            @ExpectedAnchors AS ExpectedAnchors,
            @PlacementCount AS PlacementKeyCount,
            @ExpectedPlacements AS ExpectedPlacementKeys;
        THROW 50000, 'Stop: the installed graph inventory is not 38 anchors and 65 member-placement keys.', 1;
    END;

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT
        anchor.AccountId,
        target.Email,
        N'missing-anchor-object',
        anchor.ObjectType + N' ' + CONVERT(nvarchar(36), anchor.ObjectId) + N' has no object row.'
    FROM @Anchor AS anchor
    INNER JOIN @TargetAccount AS target ON target.AccountId = anchor.AccountId
    WHERE (anchor.ObjectType = N'system' AND NOT EXISTS (SELECT 1 FROM dbo.pb_systems AS systemRow WHERE systemRow.SystemId = anchor.ObjectId))
       OR (anchor.ObjectType = N'group' AND NOT EXISTS (SELECT 1 FROM dbo.pb_groups AS groupRow WHERE groupRow.GroupId = anchor.ObjectId))
       OR (anchor.ObjectType = N'place' AND NOT EXISTS (SELECT 1 FROM dbo.pb_places AS placeRow WHERE placeRow.PlaceId = anchor.ObjectId));

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT
        placement.AccountId,
        target.Email,
        N'missing-placement-object',
        placement.SettingsKeyType + N' ' + CONVERT(nvarchar(36), placement.SettingsKey) + N' has no membership or member row.'
    FROM @Placement AS placement
    INNER JOIN @TargetAccount AS target ON target.AccountId = placement.AccountId
    WHERE (placement.SettingsKeyType = N'MemberId'
           AND NOT EXISTS (SELECT 1 FROM dbo.pb_members AS member WHERE member.MemberId = placement.SettingsKey))
       OR (placement.SettingsKeyType = N'GroupMemberId'
           AND NOT EXISTS
           (
               SELECT 1
               FROM dbo.pb_group_members AS membership
               INNER JOIN dbo.pb_members AS member ON member.MemberId = membership.MemberId
               WHERE membership.GroupMemberId = placement.SettingsKey
           ))
       OR (placement.SettingsKeyType = N'PlaceMemberId'
           AND NOT EXISTS
           (
               SELECT 1
               FROM dbo.pb_place_members AS membership
               INNER JOIN dbo.pb_members AS member ON member.MemberId = membership.MemberId
               WHERE membership.PlaceMemberId = placement.SettingsKey
           ));

    IF EXISTS (SELECT 1 FROM @Issue)
    BEGIN
        SELECT AccountId, Email, Issue, Detail FROM @Issue ORDER BY Issue, Email, Detail;
        THROW 50000, 'Stop: a target anchor or placement key does not reference an existing object.', 1;
    END;

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT
        shared.AccountId,
        target.Email,
        N'shared-target-placement-key',
        shared.SettingsKeyType + N' ' + CONVERT(nvarchar(36), shared.SettingsKey)
            + N' is reached by more than one target account.'
    FROM @Placement AS shared
    INNER JOIN @TargetAccount AS target ON target.AccountId = shared.AccountId
    WHERE EXISTS
    (
        SELECT 1
        FROM @Placement AS other
        WHERE other.SettingsKeyType = shared.SettingsKeyType
          AND other.SettingsKey = shared.SettingsKey
          AND other.AccountId <> shared.AccountId
    );

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT DISTINCT
        other.AccountId,
        otherAccount.Email,
        N'shared-excluded-placement-key',
        targetPlacement.SettingsKeyType + N' ' + CONVERT(nvarchar(36), targetPlacement.SettingsKey)
            + N' is also reached by ' + otherAccount.Email + N'.'
    FROM @Placement AS targetPlacement
    INNER JOIN @OtherKey AS other
        ON other.SettingsKeyType = targetPlacement.SettingsKeyType
       AND other.SettingsKey = targetPlacement.SettingsKey
    INNER JOIN @OtherAccount AS otherAccount ON otherAccount.AccountId = other.AccountId;

    IF NOT EXISTS (SELECT 1 FROM @MemberA)
        INSERT INTO @Issue (AccountId, Email, Issue, Detail)
        VALUES
        (
            @Example10AccountId,
            N'example-10@pluralbridge.org',
            N'missing-example10-member-a',
            N'Example 10''s graph has no member named A with explicit visibility type 3 and permission manager.'
        );

    IF EXISTS (SELECT 1 FROM @Issue)
    BEGIN
        SELECT AccountId, Email, Issue, Detail FROM @Issue ORDER BY Issue, Email, Detail;
        THROW 50000, 'Stop: a settings key is shared, or Example 10 member A could not be confirmed.', 1;
    END;

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT
        anchor.AccountId,
        target.Email,
        N'different-anchor-setting',
        anchor.ObjectType + N' ' + CONVERT(nvarchar(36), anchor.ObjectId)
            + N' stores ' + setting.VisibilityMode + N'/'
            + COALESCE(CONVERT(nvarchar(16), setting.VisibilityTypeId), N'NULL')
            + N' and ' + setting.PermissionMode + N'/'
            + COALESCE(setting.PermissionCode, N'NULL') + N'.'
    FROM @Anchor AS anchor
    INNER JOIN @TargetAccount AS target ON target.AccountId = anchor.AccountId
    INNER JOIN dbo.pb_anchor_visibility_permissions AS setting WITH (UPDLOCK, HOLDLOCK)
        ON setting.AccountId = anchor.AccountId
       AND (
            (anchor.ObjectType = N'system' AND setting.SystemId = anchor.ObjectId)
            OR (anchor.ObjectType = N'group' AND setting.GroupId = anchor.ObjectId)
            OR (anchor.ObjectType = N'place' AND setting.PlaceId = anchor.ObjectId)
       )
    WHERE NOT
    (
        setting.VisibilityMode = N'inherit'
        AND setting.VisibilityTypeId IS NULL
        AND setting.PermissionMode = N'inherit'
        AND setting.PermissionCode IS NULL
    );

    INSERT INTO @Issue (AccountId, Email, Issue, Detail)
    SELECT DISTINCT
        placement.AccountId,
        target.Email,
        N'different-placement-setting',
        placement.SettingsKeyType + N' ' + CONVERT(nvarchar(36), placement.SettingsKey)
            + N' stores ' + setting.VisibilityMode + N'/'
            + COALESCE(CONVERT(nvarchar(16), setting.VisibilityTypeId), N'NULL')
            + N' and ' + setting.PermissionMode + N'/'
            + COALESCE(setting.PermissionCode, N'NULL') + N'.'
    FROM @Placement AS placement
    INNER JOIN @TargetAccount AS target ON target.AccountId = placement.AccountId
    INNER JOIN dbo.pb_member_visibility_permissions AS setting WITH (UPDLOCK, HOLDLOCK)
        ON (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
        OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
        OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
    WHERE NOT
    (
        setting.VisibilityMode = N'inherit'
        AND setting.VisibilityTypeId IS NULL
        AND setting.PermissionMode = N'inherit'
        AND setting.PermissionCode IS NULL
    );

    IF EXISTS (SELECT 1 FROM @Issue)
    BEGIN
        SELECT AccountId, Email, Issue, Detail FROM @Issue ORDER BY Issue, Email, Detail;
        THROW 50000, 'Stop: a target settings row exists and is not inherit. It was not changed.', 1;
    END;

    SELECT
        @OutsideAnchorCountBefore = COUNT(*),
        @OutsideAnchorChecksumBefore = CHECKSUM_AGG(CHECKSUM(
            setting.AccountId, setting.SystemId, setting.GroupId, setting.PlaceId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_anchor_visibility_permissions AS setting WITH (UPDLOCK, HOLDLOCK)
    WHERE NOT EXISTS
    (
        SELECT 1 FROM @TargetAccount AS target WHERE target.AccountId = setting.AccountId
    );

    SELECT
        @Example10AnchorCountBefore = COUNT(*),
        @Example10AnchorChecksumBefore = CHECKSUM_AGG(CHECKSUM(
            setting.AccountId, setting.SystemId, setting.GroupId, setting.PlaceId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_anchor_visibility_permissions AS setting WITH (UPDLOCK, HOLDLOCK)
    WHERE setting.AccountId = @Example10AccountId;

    SELECT
        @OutsideMemberCountBefore = COUNT(*),
        @OutsideMemberChecksumBefore = CHECKSUM_AGG(CHECKSUM(
            setting.MemberId, setting.GroupMemberId, setting.PlaceMemberId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_member_visibility_permissions AS setting WITH (UPDLOCK, HOLDLOCK)
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM @Placement AS placement
        WHERE (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
           OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
           OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
    );

    SELECT
        @Example10MemberCountBefore = COUNT(*),
        @Example10MemberChecksumBefore = CHECKSUM_AGG(CHECKSUM(
            setting.MemberId, setting.GroupMemberId, setting.PlaceMemberId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_member_visibility_permissions AS setting WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN @OtherKey AS exampleKey
        ON exampleKey.AccountId = @Example10AccountId
       AND (
            (exampleKey.SettingsKeyType = N'MemberId' AND setting.MemberId = exampleKey.SettingsKey)
            OR (exampleKey.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = exampleKey.SettingsKey)
            OR (exampleKey.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = exampleKey.SettingsKey)
       );

    INSERT INTO @Report (Category, Expected, ExistingInherit, Inserted, VerifiedInherit)
    SELECT
        category.Category,
        category.Expected,
        category.ExistingInherit,
        0,
        0
    FROM
    (
        SELECT
            N'anchor-' + anchor.ObjectType AS Category,
            COUNT(*) AS Expected,
            SUM(CASE WHEN setting.AnchorVisibilityPermissionId IS NOT NULL THEN 1 ELSE 0 END) AS ExistingInherit
        FROM @Anchor AS anchor
        LEFT JOIN dbo.pb_anchor_visibility_permissions AS setting
            ON setting.AccountId = anchor.AccountId
           AND (
                (anchor.ObjectType = N'system' AND setting.SystemId = anchor.ObjectId)
                OR (anchor.ObjectType = N'group' AND setting.GroupId = anchor.ObjectId)
                OR (anchor.ObjectType = N'place' AND setting.PlaceId = anchor.ObjectId)
           )
        GROUP BY anchor.ObjectType

        UNION ALL

        SELECT
            N'placement-' + placement.SettingsKeyType,
            COUNT(*),
            SUM(CASE WHEN setting.MemberVisibilityPermissionId IS NOT NULL THEN 1 ELSE 0 END)
        FROM
        (
            SELECT DISTINCT SettingsKeyType, SettingsKey
            FROM @Placement
        ) AS placement
        LEFT JOIN dbo.pb_member_visibility_permissions AS setting
            ON (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
            OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
            OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
        GROUP BY placement.SettingsKeyType
    ) AS category;

    INSERT INTO dbo.pb_anchor_visibility_permissions
    (
        AccountId, SystemId, GroupId, PlaceId,
        VisibilityMode, VisibilityTypeId, PermissionMode, PermissionCode
    )
    SELECT
        anchor.AccountId,
        CASE WHEN anchor.ObjectType = N'system' THEN anchor.ObjectId END,
        CASE WHEN anchor.ObjectType = N'group' THEN anchor.ObjectId END,
        CASE WHEN anchor.ObjectType = N'place' THEN anchor.ObjectId END,
        N'inherit',
        NULL,
        N'inherit',
        NULL
    FROM @Anchor AS anchor
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.pb_anchor_visibility_permissions AS setting
        WHERE setting.AccountId = anchor.AccountId
          AND (
               (anchor.ObjectType = N'system' AND setting.SystemId = anchor.ObjectId)
               OR (anchor.ObjectType = N'group' AND setting.GroupId = anchor.ObjectId)
               OR (anchor.ObjectType = N'place' AND setting.PlaceId = anchor.ObjectId)
          )
    );

    INSERT INTO dbo.pb_member_visibility_permissions
    (
        MemberId, GroupMemberId, PlaceMemberId,
        VisibilityMode, VisibilityTypeId, PermissionMode, PermissionCode
    )
    SELECT
        CASE WHEN placement.SettingsKeyType = N'MemberId' THEN placement.SettingsKey END,
        CASE WHEN placement.SettingsKeyType = N'GroupMemberId' THEN placement.SettingsKey END,
        CASE WHEN placement.SettingsKeyType = N'PlaceMemberId' THEN placement.SettingsKey END,
        N'inherit',
        NULL,
        N'inherit',
        NULL
    FROM
    (
        SELECT DISTINCT SettingsKeyType, SettingsKey
        FROM @Placement
    ) AS placement
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.pb_member_visibility_permissions AS setting
        WHERE (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
           OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
           OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
    );

    UPDATE report
    SET
        VerifiedInherit = verified.VerifiedInherit,
        Inserted = verified.VerifiedInherit - report.ExistingInherit
    FROM @Report AS report
    INNER JOIN
    (
        SELECT
            N'anchor-' + anchor.ObjectType AS Category,
            SUM(CASE
                    WHEN setting.VisibilityMode = N'inherit'
                     AND setting.VisibilityTypeId IS NULL
                     AND setting.PermissionMode = N'inherit'
                     AND setting.PermissionCode IS NULL
                    THEN 1 ELSE 0 END) AS VerifiedInherit
        FROM @Anchor AS anchor
        LEFT JOIN dbo.pb_anchor_visibility_permissions AS setting
            ON setting.AccountId = anchor.AccountId
           AND (
                (anchor.ObjectType = N'system' AND setting.SystemId = anchor.ObjectId)
                OR (anchor.ObjectType = N'group' AND setting.GroupId = anchor.ObjectId)
                OR (anchor.ObjectType = N'place' AND setting.PlaceId = anchor.ObjectId)
           )
        GROUP BY anchor.ObjectType

        UNION ALL

        SELECT
            N'placement-' + placement.SettingsKeyType,
            SUM(CASE
                    WHEN setting.VisibilityMode = N'inherit'
                     AND setting.VisibilityTypeId IS NULL
                     AND setting.PermissionMode = N'inherit'
                     AND setting.PermissionCode IS NULL
                    THEN 1 ELSE 0 END)
        FROM
        (
            SELECT DISTINCT SettingsKeyType, SettingsKey
            FROM @Placement
        ) AS placement
        LEFT JOIN dbo.pb_member_visibility_permissions AS setting
            ON (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
            OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
            OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
        GROUP BY placement.SettingsKeyType
    ) AS verified
        ON verified.Category = report.Category;

    IF EXISTS
    (
        SELECT 1
        FROM @Report
        WHERE VerifiedInherit <> Expected
           OR Inserted < 0
           OR ExistingInherit + Inserted <> Expected
    )
    OR (SELECT SUM(Expected) FROM @Report WHERE Category LIKE N'anchor-%') <> @ExpectedAnchors
    OR (SELECT SUM(Expected) FROM @Report WHERE Category LIKE N'placement-%') <> @ExpectedPlacements
    BEGIN
        SELECT Category, Expected, ExistingInherit, Inserted, VerifiedInherit
        FROM @Report
        ORDER BY Category;
        THROW 50000, 'Stop: verification did not find inherit on every target key. No commit was attempted.', 1;
    END;

    SELECT
        @OutsideAnchorCountAfter = COUNT(*),
        @OutsideAnchorChecksumAfter = CHECKSUM_AGG(CHECKSUM(
            setting.AccountId, setting.SystemId, setting.GroupId, setting.PlaceId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_anchor_visibility_permissions AS setting
    WHERE NOT EXISTS
    (
        SELECT 1 FROM @TargetAccount AS target WHERE target.AccountId = setting.AccountId
    );

    SELECT
        @Example10AnchorCountAfter = COUNT(*),
        @Example10AnchorChecksumAfter = CHECKSUM_AGG(CHECKSUM(
            setting.AccountId, setting.SystemId, setting.GroupId, setting.PlaceId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_anchor_visibility_permissions AS setting
    WHERE setting.AccountId = @Example10AccountId;

    SELECT
        @OutsideMemberCountAfter = COUNT(*),
        @OutsideMemberChecksumAfter = CHECKSUM_AGG(CHECKSUM(
            setting.MemberId, setting.GroupMemberId, setting.PlaceMemberId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_member_visibility_permissions AS setting
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM @Placement AS placement
        WHERE (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
           OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
           OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
    );

    SELECT
        @Example10MemberCountAfter = COUNT(*),
        @Example10MemberChecksumAfter = CHECKSUM_AGG(CHECKSUM(
            setting.MemberId, setting.GroupMemberId, setting.PlaceMemberId,
            setting.VisibilityMode, setting.VisibilityTypeId,
            setting.PermissionMode, setting.PermissionCode))
    FROM dbo.pb_member_visibility_permissions AS setting
    INNER JOIN @OtherKey AS exampleKey
        ON exampleKey.AccountId = @Example10AccountId
       AND (
            (exampleKey.SettingsKeyType = N'MemberId' AND setting.MemberId = exampleKey.SettingsKey)
            OR (exampleKey.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = exampleKey.SettingsKey)
            OR (exampleKey.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = exampleKey.SettingsKey)
       );

    IF @OutsideAnchorCountAfter <> @OutsideAnchorCountBefore
        OR ISNULL(@OutsideAnchorChecksumAfter, -1) <> ISNULL(@OutsideAnchorChecksumBefore, -1)
        OR @Example10AnchorCountAfter <> @Example10AnchorCountBefore
        OR ISNULL(@Example10AnchorChecksumAfter, -1) <> ISNULL(@Example10AnchorChecksumBefore, -1)
        OR @OutsideMemberCountAfter <> @OutsideMemberCountBefore
        OR ISNULL(@OutsideMemberChecksumAfter, -1) <> ISNULL(@OutsideMemberChecksumBefore, -1)
        OR @Example10MemberCountAfter <> @Example10MemberCountBefore
        OR ISNULL(@Example10MemberChecksumAfter, -1) <> ISNULL(@Example10MemberChecksumBefore, -1)
        OR EXISTS
        (
            SELECT 1
            FROM @MemberA AS preserved
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.pb_member_visibility_permissions AS setting
                WHERE (
                        (preserved.SettingsKeyType = N'MemberId' AND setting.MemberId = preserved.SettingsKey)
                        OR (preserved.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = preserved.SettingsKey)
                        OR (preserved.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = preserved.SettingsKey)
                      )
                  AND setting.VisibilityMode = preserved.VisibilityMode
                  AND setting.VisibilityTypeId = preserved.VisibilityTypeId
                  AND setting.PermissionMode = preserved.PermissionMode
                  AND setting.PermissionCode = preserved.PermissionCode
            )
        )
        THROW 50000, 'Stop: Example 10 or an excluded settings row changed. No commit was attempted.', 1;

    IF @Apply = 1
    BEGIN
        COMMIT TRANSACTION;
        SET @ScriptTransaction = 0;
        SET @Outcome = N'committed';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        SET @ScriptTransaction = 0;
        SET @Outcome = N'rolled back';
    END;

    SELECT
        @Outcome AS Outcome,
        @Apply AS ApplyRequested,
        CASE
            WHEN @Outcome = N'rolled back'
            THEN N'Verification passed and the transaction was rolled back. Permanent tables are unchanged.'
            ELSE N'Verification passed and the transaction was committed.'
        END AS OutcomeDetail;

    SELECT AccountId, Email
    FROM @TargetAccount
    ORDER BY Email;

    SELECT Category, Expected, ExistingInherit, Inserted, VerifiedInherit
    FROM @Report
    ORDER BY Category;

    SELECT
        SUM(CASE WHEN Category LIKE N'anchor-%' THEN Expected ELSE 0 END) AS ExpectedAnchors,
        SUM(CASE WHEN Category LIKE N'anchor-%' THEN ExistingInherit ELSE 0 END) AS ExistingAnchorInherit,
        SUM(CASE WHEN Category LIKE N'anchor-%' THEN Inserted ELSE 0 END) AS InsertedAnchors,
        SUM(CASE WHEN Category LIKE N'anchor-%' THEN VerifiedInherit ELSE 0 END) AS VerifiedAnchors,
        SUM(CASE WHEN Category LIKE N'placement-%' THEN Expected ELSE 0 END) AS ExpectedPlacementKeys,
        SUM(CASE WHEN Category LIKE N'placement-%' THEN ExistingInherit ELSE 0 END) AS ExistingPlacementInherit,
        SUM(CASE WHEN Category LIKE N'placement-%' THEN Inserted ELSE 0 END) AS InsertedPlacementKeys,
        SUM(CASE WHEN Category LIKE N'placement-%' THEN VerifiedInherit ELSE 0 END) AS VerifiedPlacementKeys
    FROM @Report;

    SELECT
        preserved.MemberId,
        preserved.SettingsKeyType,
        preserved.SettingsKey,
        setting.VisibilityMode,
        setting.VisibilityTypeId,
        setting.PermissionMode,
        setting.PermissionCode,
        @Outcome AS Example10MemberAAfterOutcome
    FROM @MemberA AS preserved
    LEFT JOIN dbo.pb_member_visibility_permissions AS setting
        ON (preserved.SettingsKeyType = N'MemberId' AND setting.MemberId = preserved.SettingsKey)
        OR (preserved.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = preserved.SettingsKey)
        OR (preserved.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = preserved.SettingsKey);

    SELECT
        @Outcome AS Outcome,
        COUNT(*) AS LiveTargetAnchorInherit
    FROM dbo.pb_anchor_visibility_permissions AS setting
    INNER JOIN @Anchor AS anchor
        ON anchor.AccountId = setting.AccountId
       AND (
            (anchor.ObjectType = N'system' AND setting.SystemId = anchor.ObjectId)
            OR (anchor.ObjectType = N'group' AND setting.GroupId = anchor.ObjectId)
            OR (anchor.ObjectType = N'place' AND setting.PlaceId = anchor.ObjectId)
       )
    WHERE setting.VisibilityMode = N'inherit'
      AND setting.VisibilityTypeId IS NULL
      AND setting.PermissionMode = N'inherit'
      AND setting.PermissionCode IS NULL;

    SELECT
        @Outcome AS Outcome,
        COUNT(*) AS LiveTargetPlacementInherit
    FROM dbo.pb_member_visibility_permissions AS setting
    INNER JOIN
    (
        SELECT DISTINCT SettingsKeyType, SettingsKey
        FROM @Placement
    ) AS placement
        ON (placement.SettingsKeyType = N'MemberId' AND setting.MemberId = placement.SettingsKey)
        OR (placement.SettingsKeyType = N'GroupMemberId' AND setting.GroupMemberId = placement.SettingsKey)
        OR (placement.SettingsKeyType = N'PlaceMemberId' AND setting.PlaceMemberId = placement.SettingsKey)
    WHERE setting.VisibilityMode = N'inherit'
      AND setting.VisibilityTypeId IS NULL
      AND setting.PermissionMode = N'inherit'
      AND setting.PermissionCode IS NULL;
END TRY
BEGIN CATCH
    IF @ScriptTransaction = 1 AND @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    SELECT
        N'rolled back' AS Outcome,
        ERROR_NUMBER() AS ErrorNumber,
        ERROR_MESSAGE() AS ErrorMessage;

    THROW;
END CATCH;
