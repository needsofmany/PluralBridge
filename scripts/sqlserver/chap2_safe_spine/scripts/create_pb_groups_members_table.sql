USE [PluralBridgeChap2SafeSpine];
GO

IF OBJECT_ID(N'dbo.pb_group_members', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_group_members
    (
        GroupMemberId UNIQUEIDENTIFIER NOT NULL,
        SystemId UNIQUEIDENTIFIER NOT NULL,
        GroupId UNIQUEIDENTIFIER NOT NULL,
        MemberId UNIQUEIDENTIFIER NOT NULL,
        SourceGroupId NVARCHAR(128) NULL,
        SourceMemberId NVARCHAR(128) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_pb_group_members_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

SELECT
    TASK26_GROUP_MEMBERS_TABLE_EXISTS =
        CASE
            WHEN OBJECT_ID(N'dbo.pb_group_members', N'U') IS NULL THEN 0
            ELSE 1
        END;

SELECT
    COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo'
  AND TABLE_NAME = N'pb_group_members'
ORDER BY ORDINAL_POSITION;