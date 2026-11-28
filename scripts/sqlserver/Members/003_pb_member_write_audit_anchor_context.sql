-- Record of the member-write audit anchor migration already executed on
-- PluralBridgeChap2SafeSpine. Do not execute this script again.
-- CK_pb_member_write_audit_AnchorContext and CK_pb_member_write_audit_RequiresContext
-- were added WITH CHECK, so both checks are enabled and trusted.
-- FK_pb_member_write_audit_systems remains on the now-nullable SystemId.

SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

ALTER TABLE dbo.pb_member_write_audit
    ALTER COLUMN SystemId uniqueidentifier NULL;
GO

IF COL_LENGTH(N'dbo.pb_member_write_audit', N'AnchorObjectType') IS NULL
BEGIN
    ALTER TABLE dbo.pb_member_write_audit
        ADD AnchorObjectType nvarchar(64) NULL;
END;
GO

IF COL_LENGTH(N'dbo.pb_member_write_audit', N'AnchorObjectId') IS NULL
BEGIN
    ALTER TABLE dbo.pb_member_write_audit
        ADD AnchorObjectId nvarchar(128) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_pb_member_write_audit_AnchorContext'
      AND parent_object_id = OBJECT_ID(N'dbo.pb_member_write_audit')
)
BEGIN
    ALTER TABLE dbo.pb_member_write_audit WITH CHECK
        ADD CONSTRAINT CK_pb_member_write_audit_AnchorContext
        CHECK
        (
            (
                AnchorObjectType IS NULL
                AND AnchorObjectId IS NULL
            )
            OR
            (
                AnchorObjectType IN (N'system', N'group', N'place')
                AND AnchorObjectId IS NOT NULL
                AND LEN(LTRIM(RTRIM(AnchorObjectId))) > 0
            )
        );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_pb_member_write_audit_RequiresContext'
      AND parent_object_id = OBJECT_ID(N'dbo.pb_member_write_audit')
)
BEGIN
    ALTER TABLE dbo.pb_member_write_audit WITH CHECK
        ADD CONSTRAINT CK_pb_member_write_audit_RequiresContext
        CHECK
        (
            SystemId IS NOT NULL
            OR
            (
                AnchorObjectType IS NOT NULL
                AND AnchorObjectId IS NOT NULL
            )
        );
END;
GO
