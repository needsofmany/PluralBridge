-- Cloud Step 18 start
-- Azure SQL read-only connection smoke test.
-- Run in SSMS against PluralBridgeCloudProof001 on pluralbridge-cloudproof-syf001.database.windows.net.

SET NOCOUNT ON;

SELECT
    DB_NAME() AS CurrentDatabase,
    SUSER_SNAME() AS LoginName,
    SERVERPROPERTY('ServerName') AS ServerName,
    SERVERPROPERTY('EngineEdition') AS EngineEdition,
    SERVERPROPERTY('Edition') AS Edition,
    SERVERPROPERTY('ProductVersion') AS ProductVersion,
    SYSUTCDATETIME() AS CheckedAtUtc;

SELECT
    name,
    compatibility_level,
    collation_name,
    state_desc,
    containment_desc,
    create_date
FROM sys.databases
WHERE name = DB_NAME();

SELECT
    s.name AS SchemaName
FROM sys.schemas AS s
WHERE s.name = N'dbo'
ORDER BY s.name;

SELECT
    t.name AS ExistingPluralBridgeTable
FROM sys.tables AS t
WHERE t.name IN
(
    N'pb_source_systems',
    N'pb_import_batches',
    N'pb_systems',
    N'pb_members',
    N'pb_privacy_buckets',
    N'pb_custom_fields',
    N'pb_front_history',
    N'pb_source_records',
    N'pb_source_id_map'
)
ORDER BY t.name;

-- Cloud Step 18 end
