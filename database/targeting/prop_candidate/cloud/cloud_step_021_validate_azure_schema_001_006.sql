-- Cloud Step 21 start
-- Azure SQL read-only schema validation for 1-6 pb_* proof tables.
-- Run in SSMS against PluralBridgeCloudProof001 on pluralbridge-cloudproof-syf001.database.windows.net.

SET NOCOUNT ON;

DECLARE @ExpectedTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @ExpectedTables (TableName)
VALUES
    (N'pb_source_systems'),
    (N'pb_import_batches'),
    (N'pb_systems'),
    (N'pb_members'),
    (N'pb_privacy_buckets'),
    (N'pb_custom_fields'),
    (N'pb_front_history'),
    (N'pb_source_records'),
    (N'pb_source_id_map');

DECLARE @ExpectedCoreColumns TABLE (TableName sysname NOT NULL, ColumnName sysname NOT NULL, PRIMARY KEY (TableName, ColumnName));
INSERT INTO @ExpectedCoreColumns (TableName, ColumnName)
VALUES
    (N'pb_source_systems', N'SourceSystemCode'),
    (N'pb_import_batches', N'ImportBatchId'),
    (N'pb_import_batches', N'SourceSystemCode'),
    (N'pb_systems', N'SystemId'),
    (N'pb_members', N'MemberId'),
    (N'pb_members', N'SystemId'),
    (N'pb_privacy_buckets', N'PrivacyBucketId'),
    (N'pb_privacy_buckets', N'SystemId'),
    (N'pb_custom_fields', N'CustomFieldId'),
    (N'pb_custom_fields', N'SystemId'),
    (N'pb_front_history', N'FrontHistoryId'),
    (N'pb_front_history', N'SystemId'),
    (N'pb_front_history', N'MemberId'),
    (N'pb_source_records', N'SourceRecordId'),
    (N'pb_source_records', N'ImportBatchId'),
    (N'pb_source_records', N'SourceSystemCode'),
    (N'pb_source_id_map', N'SourceIdMapId'),
    (N'pb_source_id_map', N'ImportBatchId'),
    (N'pb_source_id_map', N'SourceSystemCode'),
    (N'pb_source_id_map', N'SourceEntityTypeCode'),
    (N'pb_source_id_map', N'SourceId');

DECLARE @ExpectedConstraints TABLE (ConstraintName sysname NOT NULL PRIMARY KEY, ConstraintKind nvarchar(20) NOT NULL);
INSERT INTO @ExpectedConstraints (ConstraintName, ConstraintKind)
VALUES
    (N'PK_pb_source_systems', N'PK'),
    (N'PK_pb_import_batches', N'PK'),
    (N'PK_pb_systems', N'PK'),
    (N'PK_pb_members', N'PK'),
    (N'PK_pb_privacy_buckets', N'PK'),
    (N'PK_pb_custom_fields', N'PK'),
    (N'PK_pb_front_history', N'PK'),
    (N'PK_pb_source_records', N'PK'),
    (N'PK_pb_source_id_map', N'PK'),
    (N'UQ_pb_source_id_map_SourceIdentity', N'UQ'),
    (N'FK_pb_import_batches_pb_source_systems', N'FK'),
    (N'FK_pb_members_pb_systems', N'FK'),
    (N'FK_pb_privacy_buckets_pb_systems', N'FK'),
    (N'FK_pb_custom_fields_pb_systems', N'FK'),
    (N'FK_pb_front_history_pb_systems', N'FK'),
    (N'FK_pb_front_history_pb_members', N'FK'),
    (N'FK_pb_source_records_pb_import_batches', N'FK'),
    (N'FK_pb_source_records_pb_source_systems', N'FK'),
    (N'FK_pb_source_id_map_pb_import_batches', N'FK'),
    (N'FK_pb_source_id_map_pb_source_systems', N'FK');

WITH ActualTables AS
(
    SELECT t.name AS TableName
    FROM sys.tables AS t
    WHERE t.name LIKE N'pb[_]%'
),
ActualConstraints AS
(
    SELECT kc.name AS ConstraintName, kc.type AS ConstraintKind
    FROM sys.key_constraints AS kc
    UNION ALL
    SELECT fk.name AS ConstraintName, N'FK' AS ConstraintKind
    FROM sys.foreign_keys AS fk
)
SELECT CheckName, ExpectedValue, ActualValue, Result
FROM
(
    SELECT N'Database' AS CheckName, N'PluralBridgeCloudProof001' AS ExpectedValue, CONVERT(nvarchar(200), DB_NAME()) AS ActualValue, CASE WHEN DB_NAME() = N'PluralBridgeCloudProof001' THEN N'PASS' ELSE N'FAIL' END AS Result
    UNION ALL
    SELECT N'pb_* table count', N'9', CONVERT(nvarchar(200), COUNT(*)), CASE WHEN COUNT(*) = 9 THEN N'PASS' ELSE N'FAIL' END FROM ActualTables
    UNION ALL
    SELECT N'Missing expected tables', N'0', CONVERT(nvarchar(200), COUNT(*)), CASE WHEN COUNT(*) = 0 THEN N'PASS' ELSE N'FAIL' END FROM @ExpectedTables AS e LEFT JOIN ActualTables AS a ON a.TableName = e.TableName WHERE a.TableName IS NULL
    UNION ALL
    SELECT N'Missing expected core columns', N'0', CONVERT(nvarchar(200), COUNT(*)), CASE WHEN COUNT(*) = 0 THEN N'PASS' ELSE N'FAIL' END FROM @ExpectedCoreColumns AS e LEFT JOIN sys.tables AS t ON t.name = e.TableName LEFT JOIN sys.columns AS c ON c.object_id = t.object_id AND c.name = e.ColumnName WHERE c.object_id IS NULL
    UNION ALL
    SELECT N'Primary key count', N'9', CONVERT(nvarchar(200), COUNT(*)), CASE WHEN COUNT(*) = 9 THEN N'PASS' ELSE N'FAIL' END FROM sys.key_constraints WHERE type = N'PK' AND parent_object_id IN (SELECT OBJECT_ID(N'dbo.' + TableName) FROM @ExpectedTables)
    UNION ALL
    SELECT N'Foreign key count', N'10', CONVERT(nvarchar(200), COUNT(*)), CASE WHEN COUNT(*) = 10 THEN N'PASS' ELSE N'FAIL' END FROM sys.foreign_keys WHERE parent_object_id IN (SELECT OBJECT_ID(N'dbo.' + TableName) FROM @ExpectedTables)
    UNION ALL
    SELECT N'Unique constraint count', N'1', CONVERT(nvarchar(200), COUNT(*)), CASE WHEN COUNT(*) = 1 THEN N'PASS' ELSE N'FAIL' END FROM sys.key_constraints WHERE type = N'UQ' AND parent_object_id IN (SELECT OBJECT_ID(N'dbo.' + TableName) FROM @ExpectedTables)
    UNION ALL
    SELECT N'Missing expected constraints', N'0', CONVERT(nvarchar(200), COUNT(*)), CASE WHEN COUNT(*) = 0 THEN N'PASS' ELSE N'FAIL' END FROM @ExpectedConstraints AS e LEFT JOIN ActualConstraints AS a ON a.ConstraintName = e.ConstraintName WHERE a.ConstraintName IS NULL
) AS checks
ORDER BY CheckName;

SELECT
    t.name AS TableName,
    COUNT(c.column_id) AS ColumnCount
FROM sys.tables AS t
INNER JOIN sys.columns AS c ON c.object_id = t.object_id
WHERE t.name IN (SELECT TableName FROM @ExpectedTables)
GROUP BY t.name
ORDER BY t.name;

SELECT
    ConstraintKind,
    ConstraintName
FROM @ExpectedConstraints
ORDER BY ConstraintKind, ConstraintName;

-- Cloud Step 21 end
