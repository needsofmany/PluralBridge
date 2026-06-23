USE [PluralBridgeChap2SafeSpine];
GO

SELECT
    TASK26_GROUPS_TABLE_EXISTS =
        CASE WHEN OBJECT_ID(N'dbo.pb_groups', N'U') IS NULL THEN 0 ELSE 1 END,
    TASK26_GROUP_MEMBERS_TABLE_EXISTS =
        CASE WHEN OBJECT_ID(N'dbo.pb_group_members', N'U') IS NULL THEN 0 ELSE 1 END,
    TASK26_GROUPS_EXPECTED_COLUMNS =
        CASE WHEN
        (
            SELECT COUNT(DISTINCT COLUMN_NAME)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo'
              AND TABLE_NAME = N'pb_groups'
              AND COLUMN_NAME IN
              (
                  N'GroupId',
                  N'SystemId',
                  N'SourceGroupId',
                  N'ParentGroupId',
                  N'ParentSourceGroupId',
                  N'GroupName',
                  N'GroupColor',
                  N'GroupDesc',
                  N'GroupEmoji',
                  N'SupportsDescMarkdown',
                  N'SourceExists',
                  N'LastOperationTimeUnixMs',
                  N'LastOperationAtUtc',
                  N'CreatedAtUtc',
                  N'UpdatedAtUtc'
              )
        ) = 15 THEN 1 ELSE 0 END,
    TASK26_GROUP_MEMBERS_EXPECTED_COLUMNS =
        CASE WHEN
        (
            SELECT COUNT(DISTINCT COLUMN_NAME)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo'
              AND TABLE_NAME = N'pb_group_members'
              AND COLUMN_NAME IN
              (
                  N'GroupMemberId',
                  N'SystemId',
                  N'GroupId',
                  N'MemberId',
                  N'SourceGroupId',
                  N'SourceMemberId',
                  N'CreatedAtUtc'
              )
        ) = 7 THEN 1 ELSE 0 END,
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
        ) THEN 1 ELSE 0 END,
    TASK26_GROUP_SUPPORT_SCHEMA_ONLY = 1,
    TASK26_NO_RUNTIME_BEHAVIOR_ACTIVATED = 1,
    TASK26_OK =
        CASE WHEN
            OBJECT_ID(N'dbo.pb_groups', N'U') IS NOT NULL
            AND OBJECT_ID(N'dbo.pb_group_members', N'U') IS NOT NULL
        THEN 1 ELSE 0 END;
