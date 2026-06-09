SET NOCOUNT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. This validation script must run only against PluralBridgeDemoAnonXlat.', 1;
END

PRINT '005 foreign key orphan validation';

CREATE TABLE #fk_orphan_results
(
    ForeignKeyName sysname NOT NULL,
    ChildTable nvarchar(257) NOT NULL,
    ParentTable nvarchar(257) NOT NULL,
    OrphanRows bigint NOT NULL
);

DECLARE
    @fk_object_id int,
    @fk_name sysname,
    @child_object_id int,
    @parent_object_id int,
    @child_table nvarchar(257),
    @parent_table nvarchar(257),
    @join_predicate nvarchar(max),
    @not_null_predicate nvarchar(max),
    @sql nvarchar(max);

DECLARE fk_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT fk.object_id, fk.name, fk.parent_object_id, fk.referenced_object_id,
       QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)) AS ChildTable,
       QUOTENAME(OBJECT_SCHEMA_NAME(fk.referenced_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.referenced_object_id)) AS ParentTable
FROM sys.foreign_keys fk
WHERE OBJECT_SCHEMA_NAME(fk.parent_object_id) = N'dbo'
ORDER BY fk.name;

OPEN fk_cursor;
FETCH NEXT FROM fk_cursor INTO @fk_object_id, @fk_name, @child_object_id, @parent_object_id, @child_table, @parent_table;

WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @join_predicate = STUFF((
        SELECT N' AND p.' + QUOTENAME(rc.name) + N' = c.' + QUOTENAME(pc.name)
        FROM sys.foreign_key_columns fkc
        JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        WHERE fkc.constraint_object_id = @fk_object_id
        ORDER BY fkc.constraint_column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 5, N'');

    SELECT @not_null_predicate = STUFF((
        SELECT N' OR c.' + QUOTENAME(pc.name) + N' IS NOT NULL'
        FROM sys.foreign_key_columns fkc
        JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
        WHERE fkc.constraint_object_id = @fk_object_id
        ORDER BY fkc.constraint_column_id
        FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'), 1, 4, N'');

    SET @sql = N'
        INSERT INTO #fk_orphan_results (ForeignKeyName, ChildTable, ParentTable, OrphanRows)
        SELECT @fk_name, @child_table, @parent_table, COUNT_BIG(*)
        FROM ' + @child_table + N' c
        WHERE (' + @not_null_predicate + N')
          AND NOT EXISTS
          (
              SELECT 1
              FROM ' + @parent_table + N' p
              WHERE ' + @join_predicate + N'
          );';

    EXEC sp_executesql
        @sql,
        N'@fk_name sysname, @child_table nvarchar(257), @parent_table nvarchar(257)',
        @fk_name = @fk_name,
        @child_table = @child_table,
        @parent_table = @parent_table;

    FETCH NEXT FROM fk_cursor INTO @fk_object_id, @fk_name, @child_object_id, @parent_object_id, @child_table, @parent_table;
END

CLOSE fk_cursor;
DEALLOCATE fk_cursor;

SELECT * FROM #fk_orphan_results ORDER BY ForeignKeyName;

IF EXISTS (SELECT 1 FROM #fk_orphan_results WHERE OrphanRows <> 0)
BEGIN
    THROW 51008, 'Foreign key orphan validation failed.', 1;
END

SELECT 'PASS: no foreign key orphan rows found.' as Validation_Result;
