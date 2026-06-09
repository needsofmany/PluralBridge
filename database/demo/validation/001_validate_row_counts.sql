SET NOCOUNT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. This validation script must run only against PluralBridgeDemoAnonXlat.', 1;
END

PRINT '001 row count validation';

DECLARE @expected TABLE
(
    TableName sysname NOT NULL PRIMARY KEY,
    ExpectedRows bigint NOT NULL
);

INSERT INTO @expected (TableName, ExpectedRows)
VALUES
    (N'pb_custom_fields', 7),
    (N'pb_front_history', 886),
    (N'pb_import_batches', 1),
    (N'pb_members', 49),
    (N'pb_privacy_buckets', 2),
    (N'pb_source_id_map', 945),
    (N'pb_source_records', 945),
    (N'pb_source_systems', 1),
    (N'pb_systems', 1);

WITH actual AS
(
SELECT N'pb_custom_fields' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_custom_fields]
UNION ALL
SELECT N'pb_front_history' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_front_history]
UNION ALL
SELECT N'pb_import_batches' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_import_batches]
UNION ALL
SELECT N'pb_members' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_members]
UNION ALL
SELECT N'pb_privacy_buckets' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_privacy_buckets]
UNION ALL
SELECT N'pb_source_id_map' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_source_id_map]
UNION ALL
SELECT N'pb_source_records' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_source_records]
UNION ALL
SELECT N'pb_source_systems' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_source_systems]
UNION ALL
SELECT N'pb_systems' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_systems]
)
SELECT
    e.TableName,
    e.ExpectedRows,
    a.ActualRows,
    CASE WHEN e.ExpectedRows = a.ActualRows THEN 'PASS' ELSE 'FAIL' END AS Result
FROM @expected e
LEFT JOIN actual a ON a.TableName = e.TableName
ORDER BY e.TableName;

IF EXISTS
(
    SELECT 1
    FROM @expected e
    LEFT JOIN
    (
SELECT N'pb_custom_fields' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_custom_fields]
UNION ALL
SELECT N'pb_front_history' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_front_history]
UNION ALL
SELECT N'pb_import_batches' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_import_batches]
UNION ALL
SELECT N'pb_members' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_members]
UNION ALL
SELECT N'pb_privacy_buckets' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_privacy_buckets]
UNION ALL
SELECT N'pb_source_id_map' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_source_id_map]
UNION ALL
SELECT N'pb_source_records' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_source_records]
UNION ALL
SELECT N'pb_source_systems' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_source_systems]
UNION ALL
SELECT N'pb_systems' AS TableName, COUNT_BIG(*) AS ActualRows FROM [dbo].[pb_systems]
    ) a ON a.TableName = e.TableName
    WHERE a.ActualRows IS NULL OR a.ActualRows <> e.ExpectedRows
)
BEGIN
    THROW 51001, 'Row count validation failed.', 1;
END

PRINT 'PASS: row counts match the generated master script.';
