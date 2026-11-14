-- One saved avatar per member, shared by every placement.
-- This script has not been executed.
-- Install after 001 through 003. It does not depend on 004 or 005.
-- Stored bytes are PNG, JPEG, or WebP, at most 2048 pixels on an edge
-- and at most 2 MiB. It does not change pb_members.
-- Preflight checks and schema changes run in one transaction. A failed
-- check rolls that transaction back and makes no schema change.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @ScriptTransaction bit = 0;

BEGIN TRY
    IF @@TRANCOUNT <> 0
        THROW 50000, 'Stop: this script must not run inside an existing transaction.', 1;

    BEGIN TRANSACTION;
    SET @ScriptTransaction = 1;

    IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
        THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

    IF OBJECT_ID(N'dbo.pb_member_avatars', N'U') IS NOT NULL
        THROW 50000, 'Stop: dbo.pb_member_avatars already exists.', 1;

    CREATE TABLE dbo.pb_member_avatars
    (
        MemberId uniqueidentifier NOT NULL,
        ContentType nvarchar(32) NOT NULL,
        Width int NOT NULL,
        Height int NOT NULL,
        ByteSize int NOT NULL,
        ImageBytes varbinary(max) NOT NULL,
        UpdatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_pb_member_avatars_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_pb_member_avatars
            PRIMARY KEY CLUSTERED (MemberId),

        CONSTRAINT FK_pb_member_avatars_pb_members
            FOREIGN KEY (MemberId) REFERENCES dbo.pb_members (MemberId),

        CONSTRAINT CK_pb_member_avatars_ContentType
            CHECK (ContentType IN (N'image/png', N'image/jpeg', N'image/webp')),

        CONSTRAINT CK_pb_member_avatars_Dimensions
            CHECK (Width BETWEEN 1 AND 2048 AND Height BETWEEN 1 AND 2048),

        CONSTRAINT CK_pb_member_avatars_ByteSize
            CHECK (ByteSize BETWEEN 1 AND 2097152 AND DATALENGTH(ImageBytes) = ByteSize)
    );

    COMMIT TRANSACTION;
    SET @ScriptTransaction = 0;
END TRY
BEGIN CATCH
    IF @ScriptTransaction = 1 AND @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT DB_NAME() AS CurrentDatabase;

SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS TypeName,
    c.max_length,
    c.is_nullable
FROM sys.tables AS t
INNER JOIN sys.columns AS c
    ON c.object_id = t.object_id
INNER JOIN sys.types AS ty
    ON ty.user_type_id = c.user_type_id
WHERE t.name = N'pb_member_avatars'
ORDER BY c.column_id;

SELECT
    name AS ConstraintName
FROM sys.check_constraints
WHERE parent_object_id = OBJECT_ID(N'dbo.pb_member_avatars')
ORDER BY name;

SELECT COUNT(*) AS AvatarRows
FROM dbo.pb_member_avatars;
