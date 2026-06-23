USE [PluralBridgeChap2SafeSpine];
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE [name] = N'PK_pb_groups'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_groups')
)
BEGIN
    ALTER TABLE dbo.pb_groups
    ADD CONSTRAINT PK_pb_groups
        PRIMARY KEY CLUSTERED (GroupId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE [name] = N'PK_pb_group_members'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_group_members')
)
BEGIN
    ALTER TABLE dbo.pb_group_members
    ADD CONSTRAINT PK_pb_group_members
        PRIMARY KEY CLUSTERED (GroupMemberId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_pb_groups_pb_systems_SystemId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_groups')
)
BEGIN
    ALTER TABLE dbo.pb_groups
    ADD CONSTRAINT FK_pb_groups_pb_systems_SystemId
        FOREIGN KEY (SystemId)
        REFERENCES dbo.pb_systems (SystemId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_pb_groups_pb_groups_ParentGroupId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_groups')
)
BEGIN
    ALTER TABLE dbo.pb_groups
    ADD CONSTRAINT FK_pb_groups_pb_groups_ParentGroupId
        FOREIGN KEY (ParentGroupId)
        REFERENCES dbo.pb_groups (GroupId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_pb_group_members_pb_systems_SystemId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_group_members')
)
BEGIN
    ALTER TABLE dbo.pb_group_members
    ADD CONSTRAINT FK_pb_group_members_pb_systems_SystemId
        FOREIGN KEY (SystemId)
        REFERENCES dbo.pb_systems (SystemId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_pb_group_members_pb_groups_GroupId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_group_members')
)
BEGIN
    ALTER TABLE dbo.pb_group_members
    ADD CONSTRAINT FK_pb_group_members_pb_groups_GroupId
        FOREIGN KEY (GroupId)
        REFERENCES dbo.pb_groups (GroupId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_pb_group_members_pb_members_MemberId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_group_members')
)
BEGIN
    ALTER TABLE dbo.pb_group_members
    ADD CONSTRAINT FK_pb_group_members_pb_members_MemberId
        FOREIGN KEY (MemberId)
        REFERENCES dbo.pb_members (MemberId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE [name] = N'UQ_pb_group_members_SystemId_GroupId_MemberId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_group_members')
)
BEGIN
    ALTER TABLE dbo.pb_group_members
    ADD CONSTRAINT UQ_pb_group_members_SystemId_GroupId_MemberId
        UNIQUE (SystemId, GroupId, MemberId);
END;
GO

SELECT
    TASK26_GROUPS_PK_EXISTS =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE [name] = N'PK_pb_groups'
              AND [parent_object_id] = OBJECT_ID(N'dbo.pb_groups')
        )
        THEN 1 ELSE 0 END,
    TASK26_GROUP_MEMBERS_PK_EXISTS =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE [name] = N'PK_pb_group_members'
              AND [parent_object_id] = OBJECT_ID(N'dbo.pb_group_members')
        )
        THEN 1 ELSE 0 END,
    TASK26_GROUP_FK_COUNT =
        (
            SELECT COUNT(*)
            FROM sys.foreign_keys
            WHERE [parent_object_id] IN
            (
                OBJECT_ID(N'dbo.pb_groups'),
                OBJECT_ID(N'dbo.pb_group_members')
            )
              AND [name] IN
              (
                N'FK_pb_groups_pb_systems_SystemId',
                N'FK_pb_groups_pb_groups_ParentGroupId',
                N'FK_pb_group_members_pb_systems_SystemId',
                N'FK_pb_group_members_pb_groups_GroupId',
                N'FK_pb_group_members_pb_members_MemberId'
              )
        ),
    TASK26_GROUP_UNIQUE_CONSTRAINT_EXISTS =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE [name] = N'UQ_pb_group_members_SystemId_GroupId_MemberId'
              AND [parent_object_id] = OBJECT_ID(N'dbo.pb_group_members')
        )
        THEN 1 ELSE 0 END;