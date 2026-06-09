SET NOCOUNT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. This validation script must run only against PluralBridgeDemoAnonXlat.', 1;
END

PRINT '003 key and constraint inventory validation';

DECLARE @expected_pk TABLE (TableName sysname NOT NULL, ConstraintName sysname NOT NULL, ColumnList nvarchar(max) NOT NULL);
INSERT INTO @expected_pk VALUES
    (N'pb_custom_fields', N'PK_pb_custom_fields', N'CustomFieldId'),
    (N'pb_front_history', N'PK_pb_front_history', N'FrontHistoryId'),
    (N'pb_import_batches', N'PK_pb_import_batches', N'ImportBatchId'),
    (N'pb_members', N'PK_pb_members', N'MemberId'),
    (N'pb_privacy_buckets', N'PK_pb_privacy_buckets', N'PrivacyBucketId'),
    (N'pb_source_id_map', N'PK_pb_source_id_map', N'SourceIdMapId'),
    (N'pb_source_records', N'PK_pb_source_records', N'SourceRecordId'),
    (N'pb_source_systems', N'PK_pb_source_systems', N'SourceSystemCode'),
    (N'pb_systems', N'PK_pb_systems', N'SystemId');

DECLARE @expected_fk TABLE (ConstraintName sysname NOT NULL, ChildTable sysname NOT NULL, ChildColumns nvarchar(max) NOT NULL, ParentTable sysname NOT NULL, ParentColumns nvarchar(max) NOT NULL);
INSERT INTO @expected_fk VALUES
    (N'FK_pb_custom_fields_pb_systems', N'pb_custom_fields', N'SystemId', N'pb_systems', N'SystemId'),
    (N'FK_pb_front_history_pb_members', N'pb_front_history', N'MemberId', N'pb_members', N'MemberId'),
    (N'FK_pb_front_history_pb_systems', N'pb_front_history', N'SystemId', N'pb_systems', N'SystemId'),
    (N'FK_pb_import_batches_pb_source_systems', N'pb_import_batches', N'SourceSystemCode', N'pb_source_systems', N'SourceSystemCode'),
    (N'FK_pb_members_pb_systems', N'pb_members', N'SystemId', N'pb_systems', N'SystemId'),
    (N'FK_pb_privacy_buckets_pb_systems', N'pb_privacy_buckets', N'SystemId', N'pb_systems', N'SystemId'),
    (N'FK_pb_source_id_map_pb_import_batches', N'pb_source_id_map', N'ImportBatchId', N'pb_import_batches', N'ImportBatchId'),
    (N'FK_pb_source_id_map_pb_source_systems', N'pb_source_id_map', N'SourceSystemCode', N'pb_source_systems', N'SourceSystemCode'),
    (N'FK_pb_source_records_pb_import_batches', N'pb_source_records', N'ImportBatchId', N'pb_import_batches', N'ImportBatchId'),
    (N'FK_pb_source_records_pb_source_systems', N'pb_source_records', N'SourceSystemCode', N'pb_source_systems', N'SourceSystemCode');

DECLARE @expected_uq TABLE (TableName sysname NOT NULL, ConstraintName sysname NOT NULL, ColumnList nvarchar(max) NOT NULL);
INSERT INTO @expected_uq VALUES
    (N'pb_source_id_map', N'UQ_pb_source_id_map_SourceIdentity', N'SourceSystemCode,SourceEntityTypeCode,SourceId');

DECLARE @expected_default TABLE (TableName sysname NOT NULL, ConstraintName sysname NOT NULL, ColumnName sysname NOT NULL);
INSERT INTO @expected_default VALUES
    (N'pb_custom_fields', N'DF_pb_custom_fields_CustomFieldId', N'CustomFieldId'),
    (N'pb_custom_fields', N'DF_pb_custom_fields_ImportedAtUtc', N'ImportedAtUtc'),
    (N'pb_custom_fields', N'DF_pb_custom_fields_CreatedAtUtc', N'CreatedAtUtc'),
    (N'pb_front_history', N'DF_pb_front_history_FrontHistoryId', N'FrontHistoryId'),
    (N'pb_front_history', N'DF_pb_front_history_ImportedAtUtc', N'ImportedAtUtc'),
    (N'pb_front_history', N'DF_pb_front_history_CreatedAtUtc', N'CreatedAtUtc'),
    (N'pb_import_batches', N'DF_pb_import_batches_ImportBatchId', N'ImportBatchId'),
    (N'pb_import_batches', N'DF_pb_import_batches_CreatedAtUtc', N'CreatedAtUtc'),
    (N'pb_members', N'DF_pb_members_MemberId', N'MemberId'),
    (N'pb_members', N'DF_pb_members_ImportedAtUtc', N'ImportedAtUtc'),
    (N'pb_members', N'DF_pb_members_CreatedAtUtc', N'CreatedAtUtc'),
    (N'pb_privacy_buckets', N'DF_pb_privacy_buckets_PrivacyBucketId', N'PrivacyBucketId'),
    (N'pb_privacy_buckets', N'DF_pb_privacy_buckets_ImportedAtUtc', N'ImportedAtUtc'),
    (N'pb_privacy_buckets', N'DF_pb_privacy_buckets_CreatedAtUtc', N'CreatedAtUtc'),
    (N'pb_source_id_map', N'DF_pb_source_id_map_SourceIdMapId', N'SourceIdMapId'),
    (N'pb_source_id_map', N'DF_pb_source_id_map_CreatedAtUtc', N'CreatedAtUtc'),
    (N'pb_source_records', N'DF_pb_source_records_SourceRecordId', N'SourceRecordId'),
    (N'pb_source_records', N'DF_pb_source_records_ImportedAtUtc', N'ImportedAtUtc'),
    (N'pb_source_systems', N'DF_pb_source_systems_CreatedAtUtc', N'CreatedAtUtc'),
    (N'pb_systems', N'DF_pb_systems_SystemId', N'SystemId'),
    (N'pb_systems', N'DF_pb_systems_ImportedAtUtc', N'ImportedAtUtc'),
    (N'pb_systems', N'DF_pb_systems_CreatedAtUtc', N'CreatedAtUtc');

WITH actual_pk AS
(
    SELECT
        t.name AS TableName,
        kc.name AS ConstraintName,
        STUFF((
            SELECT ',' + c2.name
            FROM sys.index_columns ic2
            JOIN sys.columns c2 ON c2.object_id = ic2.object_id AND c2.column_id = ic2.column_id
            WHERE ic2.object_id = kc.parent_object_id
              AND ic2.index_id = i.index_id
            ORDER BY ic2.key_ordinal
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 1, '') AS ColumnList
    FROM sys.key_constraints kc
    JOIN sys.tables t ON t.object_id = kc.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
    WHERE s.name = N'dbo'
      AND kc.type = 'PK'
)
SELECT 'PRIMARY_KEY' AS CheckType, e.TableName, e.ConstraintName, e.ColumnList AS ExpectedColumns, a.ColumnList AS ActualColumns,
       CASE WHEN a.ConstraintName IS NULL THEN 'FAIL_MISSING' WHEN e.ColumnList <> a.ColumnList THEN 'FAIL_COLUMNS' ELSE 'PASS' END AS Result
FROM @expected_pk e
LEFT JOIN actual_pk a ON a.TableName = e.TableName AND a.ConstraintName = e.ConstraintName
ORDER BY e.TableName;

IF EXISTS
(
    SELECT 1
    FROM @expected_pk e
    LEFT JOIN actual_pk a ON a.TableName = e.TableName AND a.ConstraintName = e.ConstraintName
    WHERE a.ConstraintName IS NULL OR e.ColumnList <> a.ColumnList
)
BEGIN
    THROW 51003, 'Primary key inventory validation failed.', 1;
END

WITH actual_fk AS
(
    SELECT
        fk.name AS ConstraintName,
        OBJECT_NAME(fk.parent_object_id) AS ChildTable,
        STUFF((
            SELECT ',' + pc.name
            FROM sys.foreign_key_columns fkc2
            JOIN sys.columns pc ON pc.object_id = fkc2.parent_object_id AND pc.column_id = fkc2.parent_column_id
            WHERE fkc2.constraint_object_id = fk.object_id
            ORDER BY fkc2.constraint_column_id
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 1, '') AS ChildColumns,
        OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
        STUFF((
            SELECT ',' + rc.name
            FROM sys.foreign_key_columns fkc2
            JOIN sys.columns rc ON rc.object_id = fkc2.referenced_object_id AND rc.column_id = fkc2.referenced_column_id
            WHERE fkc2.constraint_object_id = fk.object_id
            ORDER BY fkc2.constraint_column_id
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 1, '') AS ParentColumns,
        fk.is_disabled,
        fk.is_not_trusted
    FROM sys.foreign_keys fk
    WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) = N'dbo'
)
SELECT 'FOREIGN_KEY' AS CheckType, e.ConstraintName, e.ChildTable, e.ChildColumns AS ExpectedChildColumns, a.ChildColumns AS ActualChildColumns,
       e.ParentTable, e.ParentColumns AS ExpectedParentColumns, a.ParentColumns AS ActualParentColumns,
       a.is_disabled, a.is_not_trusted,
       CASE
           WHEN a.ConstraintName IS NULL THEN 'FAIL_MISSING'
           WHEN e.ChildColumns <> a.ChildColumns OR e.ParentTable <> a.ParentTable OR e.ParentColumns <> a.ParentColumns THEN 'FAIL_DEFINITION'
           WHEN a.is_disabled = 1 THEN 'FAIL_DISABLED'
           WHEN a.is_not_trusted = 1 THEN 'FAIL_NOT_TRUSTED'
           ELSE 'PASS'
       END AS Result
FROM @expected_fk e
LEFT JOIN actual_fk a ON a.ConstraintName = e.ConstraintName
ORDER BY e.ConstraintName;

IF EXISTS
(
    SELECT 1
    FROM @expected_fk e
    LEFT JOIN actual_fk a ON a.ConstraintName = e.ConstraintName
    WHERE a.ConstraintName IS NULL
       OR e.ChildColumns <> a.ChildColumns
       OR e.ParentTable <> a.ParentTable
       OR e.ParentColumns <> a.ParentColumns
       OR a.is_disabled = 1
       OR a.is_not_trusted = 1
)
BEGIN
    THROW 51004, 'Foreign key inventory validation failed.', 1;
END

WITH actual_uq AS
(
    SELECT
        t.name AS TableName,
        kc.name AS ConstraintName,
        STUFF((
            SELECT ',' + c2.name
            FROM sys.index_columns ic2
            JOIN sys.columns c2 ON c2.object_id = ic2.object_id AND c2.column_id = ic2.column_id
            WHERE ic2.object_id = kc.parent_object_id
              AND ic2.index_id = i.index_id
            ORDER BY ic2.key_ordinal
            FOR XML PATH(''), TYPE
        ).value('.', 'nvarchar(max)'), 1, 1, '') AS ColumnList
    FROM sys.key_constraints kc
    JOIN sys.tables t ON t.object_id = kc.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
    WHERE s.name = N'dbo'
      AND kc.type = 'UQ'
)
SELECT 'UNIQUE_CONSTRAINT' AS CheckType, e.TableName, e.ConstraintName, e.ColumnList AS ExpectedColumns, a.ColumnList AS ActualColumns,
       CASE WHEN a.ConstraintName IS NULL THEN 'FAIL_MISSING' WHEN e.ColumnList <> a.ColumnList THEN 'FAIL_COLUMNS' ELSE 'PASS' END AS Result
FROM @expected_uq e
LEFT JOIN actual_uq a ON a.TableName = e.TableName AND a.ConstraintName = e.ConstraintName
ORDER BY e.TableName;

IF EXISTS
(
    SELECT 1
    FROM @expected_uq e
    LEFT JOIN actual_uq a ON a.TableName = e.TableName AND a.ConstraintName = e.ConstraintName
    WHERE a.ConstraintName IS NULL OR e.ColumnList <> a.ColumnList
)
BEGIN
    THROW 51005, 'Unique constraint inventory validation failed.', 1;
END

WITH actual_default AS
(
    SELECT
        t.name AS TableName,
        dc.name AS ConstraintName,
        c.name AS ColumnName
    FROM sys.default_constraints dc
    JOIN sys.tables t ON t.object_id = dc.parent_object_id
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE s.name = N'dbo'
)
SELECT 'DEFAULT_CONSTRAINT' AS CheckType, e.TableName, e.ConstraintName, e.ColumnName,
       CASE WHEN a.ConstraintName IS NULL THEN 'FAIL_MISSING' ELSE 'PASS' END AS Result
FROM @expected_default e
LEFT JOIN actual_default a ON a.TableName = e.TableName AND a.ConstraintName = e.ConstraintName AND a.ColumnName = e.ColumnName
ORDER BY e.TableName, e.ColumnName;

IF EXISTS
(
    SELECT 1
    FROM @expected_default e
    LEFT JOIN actual_default a ON a.TableName = e.TableName AND a.ConstraintName = e.ConstraintName AND a.ColumnName = e.ColumnName
    WHERE a.ConstraintName IS NULL
)
BEGIN
    THROW 51006, 'Default constraint inventory validation failed.', 1;
END

SELECT 'PASS: keys and constraints match the generated master script and FKs are enabled/trusted.' as Validation_Result
