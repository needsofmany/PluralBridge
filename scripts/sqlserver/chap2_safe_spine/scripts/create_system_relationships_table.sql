USE [PluralBridgeChap2SafeSpine];
GO

IF OBJECT_ID(N'dbo.pb_system_relationships', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_system_relationships
    (
        SystemRelationshipId UNIQUEIDENTIFIER NOT NULL,
        SystemId UNIQUEIDENTIFIER NOT NULL,
        ParentSystemId UNIQUEIDENTIFIER NULL,
        RelationshipRank INT NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_pb_system_relationships_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_pb_system_relationships_UpdatedAtUtc DEFAULT (SYSUTCDATETIME())
    );
END;
GO

SELECT
    TASK27_SYSTEM_RELATIONSHIPS_TABLE_EXISTS =
        CASE
            WHEN OBJECT_ID(N'dbo.pb_system_relationships', N'U') IS NULL THEN 0
            ELSE 1
        END;

SELECT
    COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo'
  AND TABLE_NAME = N'pb_system_relationships'
ORDER BY ORDINAL_POSITION;