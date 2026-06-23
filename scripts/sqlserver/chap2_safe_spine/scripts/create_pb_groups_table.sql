USE [PluralBridgeChap2SafeSpine];
GO

IF OBJECT_ID(N'dbo.pb_groups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_groups
    (
        GroupId UNIQUEIDENTIFIER NOT NULL,
        SystemId UNIQUEIDENTIFIER NOT NULL,
        SourceGroupId NVARCHAR(128) NULL,
        ParentGroupId UNIQUEIDENTIFIER NULL,
        ParentSourceGroupId NVARCHAR(128) NULL,
        GroupName NVARCHAR(200) NOT NULL,
        GroupColor NVARCHAR(32) NULL,
        GroupDesc NVARCHAR(MAX) NULL,
        GroupEmoji NVARCHAR(64) NULL,
        SupportsDescMarkdown BIT NOT NULL CONSTRAINT DF_pb_groups_SupportsDescMarkdown DEFAULT (0),
        SourceExists BIT NOT NULL CONSTRAINT DF_pb_groups_SourceExists DEFAULT (1),
        LastOperationTimeUnixMs BIGINT NULL,
        LastOperationAtUtc DATETIME2(3) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_pb_groups_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_pb_groups_UpdatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

SELECT
    TASK26_GROUPS_TABLE_EXISTS =
        CASE
            WHEN OBJECT_ID(N'dbo.pb_groups', N'U') IS NULL THEN 0
            ELSE 1
        END;

SELECT
    COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo'
  AND TABLE_NAME = N'pb_groups'
ORDER BY ORDINAL_POSITION;
