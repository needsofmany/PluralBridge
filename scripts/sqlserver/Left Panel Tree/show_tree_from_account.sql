USE [PluralBridgeChap2SafeSpine];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Email NVARCHAR(320) = N'example-7@pluralbridge.org';

DECLARE @AccountId UNIQUEIDENTIFIER;
SELECT @AccountId = a.AccountId
FROM dbo.pb_accounts a
WHERE a.Email = @Email;

IF @AccountId IS NULL
BEGIN
    RAISERROR('Account not found for email.', 16, 1);
    RETURN;
END;

--------------------------------------------------------------------------------
-- 0) Label maps (system/group) from customField placements
--    Supports composites: "evolving, unknown"
--------------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#SystemLabels') IS NOT NULL DROP TABLE #SystemLabels;
IF OBJECT_ID('tempdb..#GroupLabels') IS NOT NULL DROP TABLE #GroupLabels;

;WITH SystemObjects AS
(
    SELECT DISTINCT TRY_CONVERT(uniqueidentifier, p.ParentObjectId) AS SystemId
    FROM dbo.pb_account_object_placements p
    WHERE p.AccountId = @AccountId
      AND p.ParentObjectType = N'system'
      AND p.ChildObjectType = N'customField'
      AND TRY_CONVERT(uniqueidentifier, p.ParentObjectId) IS NOT NULL
)
SELECT
    so.SystemId,
    LabelName =
        NULLIF(
            STUFF(
                (
                    SELECT N', ' + d.FieldName
                    FROM
                    (
                        SELECT DISTINCT cf.FieldName
                        FROM dbo.pb_account_object_placements p2
                        JOIN dbo.pb_custom_fields cf
                          ON cf.CustomFieldId = TRY_CONVERT(uniqueidentifier, p2.ChildObjectId)
                         AND cf.AccountId = p2.AccountId
                        WHERE p2.AccountId = @AccountId
                          AND p2.ParentObjectType = N'system'
                          AND p2.ChildObjectType = N'customField'
                          AND TRY_CONVERT(uniqueidentifier, p2.ParentObjectId) = so.SystemId
                          AND cf.FieldName IS NOT NULL
                    ) d
                    ORDER BY d.FieldName
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)')
            ,1,2,N'')
        ,N'')
INTO #SystemLabels
FROM SystemObjects so;

;WITH GroupObjects AS
(
    SELECT DISTINCT TRY_CONVERT(uniqueidentifier, p.ParentObjectId) AS GroupId
    FROM dbo.pb_account_object_placements p
    WHERE p.AccountId = @AccountId
      AND p.ParentObjectType = N'group'
      AND p.ChildObjectType = N'customField'
      AND TRY_CONVERT(uniqueidentifier, p.ParentObjectId) IS NOT NULL
)
SELECT
    go.GroupId,
    LabelName =
        NULLIF(
            STUFF(
                (
                    SELECT N', ' + d.FieldName
                    FROM
                    (
                        SELECT DISTINCT cf.FieldName
                        FROM dbo.pb_account_object_placements p2
                        JOIN dbo.pb_custom_fields cf
                          ON cf.CustomFieldId = TRY_CONVERT(uniqueidentifier, p2.ChildObjectId)
                         AND cf.AccountId = p2.AccountId
                        WHERE p2.AccountId = @AccountId
                          AND p2.ParentObjectType = N'group'
                          AND p2.ChildObjectType = N'customField'
                          AND TRY_CONVERT(uniqueidentifier, p2.ParentObjectId) = go.GroupId
                          AND cf.FieldName IS NOT NULL
                    ) d
                    ORDER BY d.FieldName
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)')
            ,1,2,N'')
        ,N'')
INTO #GroupLabels
FROM GroupObjects go;

--------------------------------------------------------------------------------
-- 1) Active systems in account scope
--------------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#AccountSystems') IS NOT NULL DROP TABLE #AccountSystems;
SELECT DISTINCT
    sm.SystemId,
    s.SystemName
INTO #AccountSystems
FROM dbo.pb_system_memberships sm
JOIN dbo.pb_system_membership_statuses sms
  ON sms.MembershipStatusId = sm.MembershipStatusId
JOIN dbo.pb_systems s
  ON s.SystemId = sm.SystemId
WHERE sm.AccountId = @AccountId
  AND sms.StatusName = N'Active';

IF NOT EXISTS (SELECT 1 FROM #AccountSystems)
BEGIN
    SELECT N'Account' AS TreeLine
    UNION ALL
    SELECT N'└── (no active systems)';
    RETURN;
END;

--------------------------------------------------------------------------------
-- 2) Relationship ordering
--------------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#SystemRel') IS NOT NULL DROP TABLE #SystemRel;
SELECT
    a.SystemId,
    a.SystemName,
    r.ParentSystemId,
    ISNULL(r.RelationshipRank, 1) AS RelationshipRank
INTO #SystemRel
FROM #AccountSystems a
LEFT JOIN dbo.pb_system_relationships r
  ON r.SystemId = a.SystemId;

IF OBJECT_ID('tempdb..#SystemRelOrdered') IS NOT NULL DROP TABLE #SystemRelOrdered;
SELECT
    sr.SystemId,
    sr.SystemName,
    sr.ParentSystemId,
    sr.RelationshipRank,
    ROW_NUMBER() OVER (
        PARTITION BY sr.ParentSystemId
        ORDER BY sr.RelationshipRank, sr.SystemName, sr.SystemId
    ) AS SiblingOrder
INTO #SystemRelOrdered
FROM #SystemRel sr;

--------------------------------------------------------------------------------
-- 3) Hierarchical system traversal
--------------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#SystemTree') IS NOT NULL DROP TABLE #SystemTree;
;WITH SystemTree AS
(
    SELECT
        ro.SystemId,
        ro.SystemName,
        ro.ParentSystemId,
        ro.RelationshipRank,
        ro.SiblingOrder,
        CAST(RIGHT('000000' + CAST(ro.SiblingOrder AS VARCHAR(6)), 6) AS NVARCHAR(4000)) AS PathKey
    FROM #SystemRelOrdered ro
    WHERE ro.ParentSystemId IS NULL
       OR NOT EXISTS (SELECT 1 FROM #AccountSystems p WHERE p.SystemId = ro.ParentSystemId)

    UNION ALL

    SELECT
        c.SystemId,
        c.SystemName,
        c.ParentSystemId,
        c.RelationshipRank,
        c.SiblingOrder,
        CAST(st.PathKey + N'.' + RIGHT('000000' + CAST(c.SiblingOrder AS VARCHAR(6)), 6) AS NVARCHAR(4000)) AS PathKey
    FROM #SystemRelOrdered c
    JOIN SystemTree st
      ON c.ParentSystemId = st.SystemId
)
SELECT
    SystemId,
    SystemName,
    ParentSystemId,
    RelationshipRank,
    SiblingOrder,
    PathKey
INTO #SystemTree
FROM SystemTree
OPTION (MAXRECURSION 100);

--------------------------------------------------------------------------------
-- 4) Generic render nodes
--------------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#Nodes') IS NOT NULL DROP TABLE #Nodes;
CREATE TABLE #Nodes
(
    NodeId INT IDENTITY(1,1) PRIMARY KEY,
    ParentNodeId INT NULL,
    Label NVARCHAR(500) NOT NULL,
    SortKey INT NOT NULL,
    Kind NVARCHAR(40) NOT NULL,
    RefSystemId UNIQUEIDENTIFIER NULL,
    RefGroupId UNIQUEIDENTIFIER NULL
);

DECLARE @AccountNodeId INT;
INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
VALUES (NULL, N'Account', 0, N'account', NULL, NULL);
SET @AccountNodeId = SCOPE_IDENTITY();

IF OBJECT_ID('tempdb..#SystemNodeMap') IS NOT NULL DROP TABLE #SystemNodeMap;
CREATE TABLE #SystemNodeMap
(
    SystemId UNIQUEIDENTIFIER PRIMARY KEY,
    NodeId INT NOT NULL
);

--------------------------------------------------------------------------------
-- 5) Insert systems with optional labels
--------------------------------------------------------------------------------
DECLARE @SysId UNIQUEIDENTIFIER;
DECLARE @SysName NVARCHAR(1020);
DECLARE @ParentSysId UNIQUEIDENTIFIER;
DECLARE @SysSiblingOrder INT;
DECLARE @ParentSystemNodeId INT;
DECLARE @InsertedSystemNodeId INT;
DECLARE @SystemSortKey INT;
DECLARE @SystemLabel NVARCHAR(MAX);
DECLARE @SystemDisplay NVARCHAR(500);

DECLARE sys_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT st.SystemId, st.SystemName, st.ParentSystemId, st.SiblingOrder
FROM #SystemTree st
ORDER BY st.PathKey;

OPEN sys_cursor;
FETCH NEXT FROM sys_cursor INTO @SysId, @SysName, @ParentSysId, @SysSiblingOrder;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @ParentSystemNodeId = NULL;
    IF @ParentSysId IS NOT NULL
    BEGIN
        SELECT @ParentSystemNodeId = snm.NodeId
        FROM #SystemNodeMap snm
        WHERE snm.SystemId = @ParentSysId;
    END

    SET @SystemLabel = NULL;
    SELECT @SystemLabel = sl.LabelName
    FROM #SystemLabels sl
    WHERE sl.SystemId = @SysId;

    SET @SystemDisplay = ISNULL(@SysName, N'(unnamed system)')
        + CASE WHEN @SystemLabel IS NOT NULL THEN N' (' + @SystemLabel + N')' ELSE N'' END;

    IF @ParentSystemNodeId IS NULL
    BEGIN
        SET @SystemSortKey = ISNULL(@SysSiblingOrder, 1);
        INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
        VALUES (@AccountNodeId, N'System: ' + @SystemDisplay, @SystemSortKey, N'system', @SysId, NULL);
    END
    ELSE
    BEGIN
        SET @SystemSortKey = 5000 + ISNULL(@SysSiblingOrder, 1);
        INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
        VALUES (@ParentSystemNodeId, N'Child System: ' + @SystemDisplay, @SystemSortKey, N'system', @SysId, NULL);
    END

    SET @InsertedSystemNodeId = SCOPE_IDENTITY();
    INSERT INTO #SystemNodeMap (SystemId, NodeId) VALUES (@SysId, @InsertedSystemNodeId);

    FETCH NEXT FROM sys_cursor INTO @SysId, @SysName, @ParentSysId, @SysSiblingOrder;
END

CLOSE sys_cursor;
DEALLOCATE sys_cursor;

--------------------------------------------------------------------------------
-- 6) Account-level Notes / Front history
--------------------------------------------------------------------------------
IF EXISTS
(
    SELECT 1
    FROM dbo.pb_account_object_links aol
    WHERE aol.AccountId = @AccountId
      AND aol.ObjectType = N'note'
)
BEGIN
    INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
    VALUES (@AccountNodeId, N'Notes', 9000, N'accountNotes', NULL, NULL);
END

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_account_object_links aol
    WHERE aol.AccountId = @AccountId
      AND aol.ObjectType = N'frontHistory'
)
BEGIN
    INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
    VALUES (@AccountNodeId, N'Front history', 9001, N'accountFrontHistory', NULL, NULL);
END

--------------------------------------------------------------------------------
-- 7) Per-system children: members + groups
--------------------------------------------------------------------------------
DECLARE @SystemNodeId INT;
DECLARE sys_children_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT snm.SystemId, snm.NodeId
FROM #SystemNodeMap snm;

OPEN sys_children_cursor;
FETCH NEXT FROM sys_children_cursor INTO @SysId, @SystemNodeId;

WHILE @@FETCH_STATUS = 0
BEGIN
    INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
    SELECT
        @SystemNodeId,
        N'Member: ' + ISNULL(m.DisplayName, N'(unnamed member)'),
        1000 + ROW_NUMBER() OVER (ORDER BY m.DisplayName, m.MemberId),
        N'member',
        @SysId,
        NULL
    FROM dbo.pb_members m
    WHERE m.SystemId = @SysId;

    DECLARE @GroupsHeaderNodeId INT;
    SET @GroupsHeaderNodeId = NULL;

    IF EXISTS (SELECT 1 FROM dbo.pb_groups g WHERE g.SystemId = @SysId)
    BEGIN
        INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
        VALUES (@SystemNodeId, N'Groups', 2000, N'groupsHeader', @SysId, NULL);
        SET @GroupsHeaderNodeId = SCOPE_IDENTITY();
    END

    IF @GroupsHeaderNodeId IS NOT NULL
    BEGIN
        IF OBJECT_ID('tempdb..#GroupResolved') IS NOT NULL DROP TABLE #GroupResolved;
        SELECT
            g.GroupId,
            g.GroupName,
            COALESCE(g.ParentGroupId, gp.GroupId) AS ResolvedParentGroupId
        INTO #GroupResolved
        FROM dbo.pb_groups g
        LEFT JOIN dbo.pb_groups gp
          ON gp.SystemId = g.SystemId
         AND gp.SourceGroupId = g.ParentSourceGroupId
        WHERE g.SystemId = @SysId;

        IF OBJECT_ID('tempdb..#GroupOrdered') IS NOT NULL DROP TABLE #GroupOrdered;
        SELECT
            gr.GroupId,
            gr.GroupName,
            gr.ResolvedParentGroupId,
            ROW_NUMBER() OVER (
                PARTITION BY gr.ResolvedParentGroupId
                ORDER BY gr.GroupName, gr.GroupId
            ) AS SiblingOrder
        INTO #GroupOrdered
        FROM #GroupResolved gr;

        IF OBJECT_ID('tempdb..#GroupTree') IS NOT NULL DROP TABLE #GroupTree;
        ;WITH GroupTree AS
        (
            SELECT
                go.GroupId,
                go.GroupName,
                go.ResolvedParentGroupId,
                go.SiblingOrder,
                CAST(RIGHT('000000' + CAST(go.SiblingOrder AS VARCHAR(6)), 6) AS NVARCHAR(4000)) AS PathKey
            FROM #GroupOrdered go
            WHERE go.ResolvedParentGroupId IS NULL
               OR NOT EXISTS (SELECT 1 FROM #GroupResolved p WHERE p.GroupId = go.ResolvedParentGroupId)

            UNION ALL

            SELECT
                c.GroupId,
                c.GroupName,
                c.ResolvedParentGroupId,
                c.SiblingOrder,
                CAST(gt.PathKey + N'.' + RIGHT('000000' + CAST(c.SiblingOrder AS VARCHAR(6)), 6) AS NVARCHAR(4000)) AS PathKey
            FROM #GroupOrdered c
            JOIN GroupTree gt
              ON c.ResolvedParentGroupId = gt.GroupId
        )
        SELECT
            GroupId,
            GroupName,
            ResolvedParentGroupId,
            SiblingOrder,
            PathKey
        INTO #GroupTree
        FROM GroupTree
        OPTION (MAXRECURSION 100);

        IF OBJECT_ID('tempdb..#GroupNodeMap') IS NOT NULL DROP TABLE #GroupNodeMap;
        CREATE TABLE #GroupNodeMap
        (
            GroupId UNIQUEIDENTIFIER PRIMARY KEY,
            NodeId INT NOT NULL
        );

        DECLARE @GroupId UNIQUEIDENTIFIER;
        DECLARE @GroupName NVARCHAR(1020);
        DECLARE @ParentGroupId UNIQUEIDENTIFIER;
        DECLARE @GroupSiblingOrder INT;
        DECLARE @ParentGroupNodeId INT;
        DECLARE @InsertedGroupNodeId INT;
        DECLARE @GroupLabel NVARCHAR(MAX);
        DECLARE @GroupDisplay NVARCHAR(500);

        DECLARE grp_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT gt.GroupId, gt.GroupName, gt.ResolvedParentGroupId, gt.SiblingOrder
        FROM #GroupTree gt
        ORDER BY gt.PathKey;

        OPEN grp_cursor;
        FETCH NEXT FROM grp_cursor INTO @GroupId, @GroupName, @ParentGroupId, @GroupSiblingOrder;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @ParentGroupNodeId = NULL;
            IF @ParentGroupId IS NOT NULL
            BEGIN
                SELECT @ParentGroupNodeId = gnm.NodeId
                FROM #GroupNodeMap gnm
                WHERE gnm.GroupId = @ParentGroupId;
            END

            SET @GroupLabel = NULL;
            SELECT @GroupLabel = gl.LabelName
            FROM #GroupLabels gl
            WHERE gl.GroupId = @GroupId;

            SET @GroupDisplay = ISNULL(@GroupName, N'(unnamed group)')
                + CASE WHEN @GroupLabel IS NOT NULL THEN N' (' + @GroupLabel + N')' ELSE N'' END;

            IF @ParentGroupNodeId IS NULL
            BEGIN
                INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
                VALUES (@GroupsHeaderNodeId, N'Group: ' + @GroupDisplay, 2000 + ISNULL(@GroupSiblingOrder, 1), N'group', @SysId, @GroupId);
            END
            ELSE
            BEGIN
                INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
                VALUES (@ParentGroupNodeId, N'Group: ' + @GroupDisplay, 2000 + ISNULL(@GroupSiblingOrder, 1), N'group', @SysId, @GroupId);
            END

            SET @InsertedGroupNodeId = SCOPE_IDENTITY();
            INSERT INTO #GroupNodeMap (GroupId, NodeId) VALUES (@GroupId, @InsertedGroupNodeId);

            INSERT INTO #Nodes (ParentNodeId, Label, SortKey, Kind, RefSystemId, RefGroupId)
            SELECT
                @InsertedGroupNodeId,
                N'Member: ' + ISNULL(m.DisplayName, N'(unnamed member)'),
                1000 + ROW_NUMBER() OVER (ORDER BY m.DisplayName, m.MemberId),
                N'groupMember',
                @SysId,
                @GroupId
            FROM dbo.pb_group_members gm
            JOIN dbo.pb_members m
              ON m.MemberId = gm.MemberId
            WHERE gm.GroupId = @GroupId;

            FETCH NEXT FROM grp_cursor INTO @GroupId, @GroupName, @ParentGroupId, @GroupSiblingOrder;
        END

        CLOSE grp_cursor;
        DEALLOCATE grp_cursor;
    END

    FETCH NEXT FROM sys_children_cursor INTO @SysId, @SystemNodeId;
END

CLOSE sys_children_cursor;
DEALLOCATE sys_children_cursor;

--------------------------------------------------------------------------------
-- 8) Render tree with connectors
--------------------------------------------------------------------------------
;WITH Ordered AS
(
    SELECT
        n.NodeId,
        n.ParentNodeId,
        n.Label,
        n.SortKey,
        ROW_NUMBER() OVER (
            PARTITION BY n.ParentNodeId
            ORDER BY n.SortKey, n.Label, n.NodeId
        ) AS rn,
        COUNT(*) OVER (PARTITION BY n.ParentNodeId) AS cnt
    FROM #Nodes n
),
Tree AS
(
    SELECT
        o.NodeId,
        o.ParentNodeId,
        o.Label,
        CAST(N'' AS NVARCHAR(MAX)) AS PrefixForThisNode,
        CAST(N'' AS NVARCHAR(MAX)) AS PrefixForChildren,
        CAST(N'000000' AS NVARCHAR(MAX)) AS PathKey
    FROM Ordered o
    WHERE o.ParentNodeId IS NULL

    UNION ALL

    SELECT
        c.NodeId,
        c.ParentNodeId,
        c.Label,
        CAST(t.PrefixForChildren + CASE WHEN c.rn < c.cnt THEN N'├── ' ELSE N'└── ' END AS NVARCHAR(MAX)),
        CAST(t.PrefixForChildren + CASE WHEN c.rn < c.cnt THEN N'│   ' ELSE N'    ' END AS NVARCHAR(MAX)),
        CAST(t.PathKey + N'.' + RIGHT('000000' + CAST(c.rn AS VARCHAR(6)), 6) AS NVARCHAR(MAX))
    FROM Ordered c
    JOIN Tree t
      ON c.ParentNodeId = t.NodeId
)
SELECT
    CASE WHEN ParentNodeId IS NULL THEN Label ELSE PrefixForThisNode + Label END AS TreeLine
FROM Tree
ORDER BY PathKey
OPTION (MAXRECURSION 32767);