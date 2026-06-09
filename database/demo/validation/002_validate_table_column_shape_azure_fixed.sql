SET NOCOUNT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. This validation script must run only against PluralBridgeDemoAnonXlat.', 1;
END

SELECT '002 table and column shape validation' AS validation_step;

DECLARE @expected TABLE
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    ColumnOrdinal int NOT NULL,
    TypeText nvarchar(128) NOT NULL,
    IsNullable bit NOT NULL,
    PRIMARY KEY (TableName, ColumnName)
);

INSERT INTO @expected (TableName, ColumnName, ColumnOrdinal, TypeText, IsNullable)
VALUES
    (N'pb_custom_fields', N'CustomFieldId', 1, N'uniqueidentifier', 0),
    (N'pb_custom_fields', N'SystemId', 2, N'uniqueidentifier', 0),
    (N'pb_custom_fields', N'FieldName', 3, N'nvarchar(510)', 0),
    (N'pb_custom_fields', N'Description', 4, N'nvarchar(max)', 1),
    (N'pb_custom_fields', N'FieldTypeCode', 5, N'int', 1),
    (N'pb_custom_fields', N'DisplayOrderText', 6, N'nvarchar(128)', 1),
    (N'pb_custom_fields', N'SupportsMarkdown', 7, N'bit', 1),
    (N'pb_custom_fields', N'ImportedAtUtc', 8, N'datetime2(3)', 0),
    (N'pb_custom_fields', N'CreatedAtUtc', 9, N'datetime2(3)', 0),
    (N'pb_custom_fields', N'UpdatedAtUtc', 10, N'datetime2(3)', 1),
    (N'pb_front_history', N'FrontHistoryId', 1, N'uniqueidentifier', 0),
    (N'pb_front_history', N'SystemId', 2, N'uniqueidentifier', 0),
    (N'pb_front_history', N'MemberId', 3, N'uniqueidentifier', 0),
    (N'pb_front_history', N'StartTimeMs', 4, N'bigint', 0),
    (N'pb_front_history', N'EndTimeMs', 5, N'bigint', 1),
    (N'pb_front_history', N'IsLive', 6, N'bit', 1),
    (N'pb_front_history', N'IsCustom', 7, N'bit', 1),
    (N'pb_front_history', N'CustomStatus', 8, N'nvarchar(510)', 1),
    (N'pb_front_history', N'LastOperationTimeMs', 9, N'bigint', 1),
    (N'pb_front_history', N'ImportedAtUtc', 10, N'datetime2(3)', 0),
    (N'pb_front_history', N'CreatedAtUtc', 11, N'datetime2(3)', 0),
    (N'pb_front_history', N'UpdatedAtUtc', 12, N'datetime2(3)', 1),
    (N'pb_import_batches', N'ImportBatchId', 1, N'uniqueidentifier', 0),
    (N'pb_import_batches', N'SourceSystemCode', 2, N'nvarchar(64)', 0),
    (N'pb_import_batches', N'ImportStartedAtUtc', 3, N'datetime2(3)', 0),
    (N'pb_import_batches', N'ImportCompletedAtUtc', 4, N'datetime2(3)', 1),
    (N'pb_import_batches', N'ImportToolName', 5, N'nvarchar(510)', 1),
    (N'pb_import_batches', N'ImportToolVersion', 6, N'nvarchar(128)', 1),
    (N'pb_import_batches', N'SourceExportName', 7, N'nvarchar(1000)', 1),
    (N'pb_import_batches', N'SourceExportSha256', 8, N'varbinary(32)', 1),
    (N'pb_import_batches', N'Notes', 9, N'nvarchar(max)', 1),
    (N'pb_import_batches', N'CreatedAtUtc', 10, N'datetime2(3)', 0),
    (N'pb_members', N'MemberId', 1, N'uniqueidentifier', 0),
    (N'pb_members', N'SystemId', 2, N'uniqueidentifier', 0),
    (N'pb_members', N'DisplayName', 3, N'nvarchar(510)', 0),
    (N'pb_members', N'Pronouns', 4, N'nvarchar(510)', 1),
    (N'pb_members', N'Description', 5, N'nvarchar(max)', 1),
    (N'pb_members', N'Color', 6, N'nvarchar(64)', 1),
    (N'pb_members', N'IsArchived', 7, N'bit', 1),
    (N'pb_members', N'ArchivedReason', 8, N'nvarchar(max)', 1),
    (N'pb_members', N'IsPrivate', 9, N'bit', 1),
    (N'pb_members', N'PreventTrusted', 10, N'bit', 1),
    (N'pb_members', N'PreventsFrontNotifications', 11, N'bit', 1),
    (N'pb_members', N'ReceiveMessageBoardNotifications', 12, N'bit', 1),
    (N'pb_members', N'SupportsDescriptionMarkdown', 13, N'bit', 1),
    (N'pb_members', N'LastOperationTimeMs', 14, N'bigint', 1),
    (N'pb_members', N'ImportedAtUtc', 15, N'datetime2(3)', 0),
    (N'pb_members', N'CreatedAtUtc', 16, N'datetime2(3)', 0),
    (N'pb_members', N'UpdatedAtUtc', 17, N'datetime2(3)', 1),
    (N'pb_privacy_buckets', N'PrivacyBucketId', 1, N'uniqueidentifier', 0),
    (N'pb_privacy_buckets', N'SystemId', 2, N'uniqueidentifier', 0),
    (N'pb_privacy_buckets', N'BucketName', 3, N'nvarchar(510)', 0),
    (N'pb_privacy_buckets', N'Description', 4, N'nvarchar(max)', 1),
    (N'pb_privacy_buckets', N'Color', 5, N'nvarchar(64)', 1),
    (N'pb_privacy_buckets', N'Icon', 6, N'nvarchar(510)', 1),
    (N'pb_privacy_buckets', N'RankText', 7, N'nvarchar(128)', 1),
    (N'pb_privacy_buckets', N'ImportedAtUtc', 8, N'datetime2(3)', 0),
    (N'pb_privacy_buckets', N'CreatedAtUtc', 9, N'datetime2(3)', 0),
    (N'pb_privacy_buckets', N'UpdatedAtUtc', 10, N'datetime2(3)', 1),
    (N'pb_source_id_map', N'SourceIdMapId', 1, N'uniqueidentifier', 0),
    (N'pb_source_id_map', N'SourceSystemCode', 2, N'nvarchar(64)', 0),
    (N'pb_source_id_map', N'SourceEntityTypeCode', 3, N'nvarchar(128)', 0),
    (N'pb_source_id_map', N'SourceId', 4, N'nvarchar(256)', 0),
    (N'pb_source_id_map', N'PluralBridgeEntityTypeCode', 5, N'nvarchar(128)', 0),
    (N'pb_source_id_map', N'PluralBridgeId', 6, N'uniqueidentifier', 0),
    (N'pb_source_id_map', N'ImportBatchId', 7, N'uniqueidentifier', 0),
    (N'pb_source_id_map', N'CreatedAtUtc', 8, N'datetime2(3)', 0),
    (N'pb_source_records', N'SourceRecordId', 1, N'uniqueidentifier', 0),
    (N'pb_source_records', N'ImportBatchId', 2, N'uniqueidentifier', 0),
    (N'pb_source_records', N'SourceSystemCode', 3, N'nvarchar(64)', 0),
    (N'pb_source_records', N'SourceEntityTypeCode', 4, N'nvarchar(128)', 0),
    (N'pb_source_records', N'SourceId', 5, N'nvarchar(256)', 1),
    (N'pb_source_records', N'SourceEndpoint', 6, N'nvarchar(2000)', 1),
    (N'pb_source_records', N'RawJson', 7, N'nvarchar(max)', 1),
    (N'pb_source_records', N'RawJsonSha256', 8, N'varbinary(32)', 1),
    (N'pb_source_records', N'ImportedAtUtc', 9, N'datetime2(3)', 0),
    (N'pb_source_systems', N'SourceSystemCode', 1, N'nvarchar(64)', 0),
    (N'pb_source_systems', N'DisplayName', 2, N'nvarchar(510)', 0),
    (N'pb_source_systems', N'Description', 3, N'nvarchar(max)', 1),
    (N'pb_source_systems', N'ApiBaseUrl', 4, N'nvarchar(2000)', 1),
    (N'pb_source_systems', N'CreatedAtUtc', 5, N'datetime2(3)', 0),
    (N'pb_systems', N'SystemId', 1, N'uniqueidentifier', 0),
    (N'pb_systems', N'SystemName', 2, N'nvarchar(510)', 1),
    (N'pb_systems', N'Description', 3, N'nvarchar(max)', 1),
    (N'pb_systems', N'Color', 4, N'nvarchar(64)', 1),
    (N'pb_systems', N'AvatarUrl', 5, N'nvarchar(2000)', 1),
    (N'pb_systems', N'AvatarUuid', 6, N'nvarchar(128)', 1),
    (N'pb_systems', N'SourceCreatedAtMs', 7, N'bigint', 1),
    (N'pb_systems', N'LastOperationTimeMs', 8, N'bigint', 1),
    (N'pb_systems', N'ImportedAtUtc', 9, N'datetime2(3)', 0),
    (N'pb_systems', N'CreatedAtUtc', 10, N'datetime2(3)', 0),
    (N'pb_systems', N'UpdatedAtUtc', 11, N'datetime2(3)', 1);

DECLARE @actual TABLE
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    ColumnOrdinal int NOT NULL,
    TypeText nvarchar(128) NOT NULL,
    IsNullable bit NOT NULL,
    PRIMARY KEY (TableName, ColumnName)
);

INSERT INTO @actual (TableName, ColumnName, ColumnOrdinal, TypeText, IsNullable)
SELECT
        t.name AS TableName,
        c.name AS ColumnName,
        c.column_id AS ColumnOrdinal,
        LOWER(TYPE_NAME(c.user_type_id)) +
            CASE
                WHEN TYPE_NAME(c.user_type_id) IN ('nvarchar', 'nchar') THEN
                    CASE WHEN c.max_length = -1 THEN '(max)' ELSE '(' + CONVERT(varchar(20), c.max_length / 2) + ')' END
                WHEN TYPE_NAME(c.user_type_id) IN ('varchar', 'char', 'varbinary', 'binary') THEN
                    CASE WHEN c.max_length = -1 THEN '(max)' ELSE '(' + CONVERT(varchar(20), c.max_length) + ')' END
                WHEN TYPE_NAME(c.user_type_id) IN ('decimal', 'numeric') THEN
                    '(' + CONVERT(varchar(20), c.precision) + ',' + CONVERT(varchar(20), c.scale) + ')'
                WHEN TYPE_NAME(c.user_type_id) IN ('datetime2', 'datetimeoffset', 'time') THEN
                    '(' + CONVERT(varchar(20), c.scale) + ')'
                ELSE ''
            END AS TypeText,
        c.is_nullable AS IsNullable
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id
    WHERE s.name = N'dbo'
      AND t.name IN (N'pb_custom_fields', N'pb_front_history', N'pb_import_batches', N'pb_members', N'pb_privacy_buckets', N'pb_source_id_map', N'pb_source_records', N'pb_source_systems', N'pb_systems');

SELECT
    COALESCE(e.TableName, a.TableName) AS TableName,
    COALESCE(e.ColumnName, a.ColumnName) AS ColumnName,
    e.ColumnOrdinal AS ExpectedOrdinal,
    a.ColumnOrdinal AS ActualOrdinal,
    e.TypeText AS ExpectedType,
    a.TypeText AS ActualType,
    e.IsNullable AS ExpectedNullable,
    a.IsNullable AS ActualNullable,
    CASE
        WHEN e.TableName IS NULL THEN 'FAIL_EXTRA_COLUMN'
        WHEN a.TableName IS NULL THEN 'FAIL_MISSING_COLUMN'
        WHEN e.TypeText <> a.TypeText THEN 'FAIL_TYPE'
        WHEN e.IsNullable <> a.IsNullable THEN 'FAIL_NULLABILITY'
        ELSE 'PASS'
    END AS Result
FROM @expected e
FULL OUTER JOIN @actual a
    ON a.TableName = e.TableName
   AND a.ColumnName = e.ColumnName
ORDER BY COALESCE(e.TableName, a.TableName), COALESCE(e.ColumnOrdinal, a.ColumnOrdinal, 9999), COALESCE(e.ColumnName, a.ColumnName);

IF EXISTS
(
    SELECT 1
    FROM @expected e
    FULL OUTER JOIN @actual a
        ON a.TableName = e.TableName
       AND a.ColumnName = e.ColumnName
    WHERE e.TableName IS NULL
       OR a.TableName IS NULL
       OR e.TypeText <> a.TypeText
       OR e.IsNullable <> a.IsNullable
)
BEGIN
    THROW 51002, 'Table/column shape validation failed.', 1;
END

SELECT 'PASS: table and column shape match the generated master script.' AS validation_result;
