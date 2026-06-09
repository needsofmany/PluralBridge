SET NOCOUNT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. This validation script must run only against PluralBridgeDemoAnonXlat.', 1;
END

PRINT '006 uniqueidentifier profile';

CREATE TABLE #guid_profile
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    TotalRows bigint NOT NULL,
    NullRows bigint NOT NULL,
    NonNullRows bigint NOT NULL,
    DistinctNonNullValues bigint NOT NULL
);

DECLARE
    @table_name sysname,
    @column_name sysname,
    @sql nvarchar(max);

DECLARE guid_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name, c.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE s.name = N'dbo'
  AND ty.name = N'uniqueidentifier'
ORDER BY t.name, c.column_id;

OPEN guid_cursor;
FETCH NEXT FROM guid_cursor INTO @table_name, @column_name;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
        INSERT INTO #guid_profile (TableName, ColumnName, TotalRows, NullRows, NonNullRows, DistinctNonNullValues)
        SELECT
            @table_name,
            @column_name,
            COUNT_BIG(*),
            SUM(CASE WHEN ' + QUOTENAME(@column_name) + N' IS NULL THEN 1 ELSE 0 END),
            SUM(CASE WHEN ' + QUOTENAME(@column_name) + N' IS NOT NULL THEN 1 ELSE 0 END),
            COUNT_BIG(DISTINCT ' + QUOTENAME(@column_name) + N')
        FROM [dbo].' + QUOTENAME(@table_name) + N';';

    EXEC sp_executesql
        @sql,
        N'@table_name sysname, @column_name sysname',
        @table_name = @table_name,
        @column_name = @column_name;

    FETCH NEXT FROM guid_cursor INTO @table_name, @column_name;
END

CLOSE guid_cursor;
DEALLOCATE guid_cursor;

SELECT *
FROM #guid_profile
ORDER BY TableName, ColumnName;

SELECT 'PASS: uniqueidentifier profile completed. Review distinct/non-null counts for reasonableness.' as Validation_Result;
