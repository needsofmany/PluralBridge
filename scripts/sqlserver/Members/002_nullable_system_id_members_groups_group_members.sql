-- Record of the nullable SystemId migration already executed on
-- PluralBridgeChap2SafeSpine. Do not execute this script again.
-- Existing values, foreign keys, and group-membership uniqueness were left in place.

SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

ALTER TABLE dbo.pb_members
    ALTER COLUMN SystemId uniqueidentifier NULL;
GO

ALTER TABLE dbo.pb_groups
    ALTER COLUMN SystemId uniqueidentifier NULL;
GO

ALTER TABLE dbo.pb_group_members
    ALTER COLUMN SystemId uniqueidentifier NULL;
GO
