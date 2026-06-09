-- 007_final_text_residue_audit.sql
-- Purpose: Read-only audit for remaining private-data residue in text-bearing columns.
-- Target database: PluralBridgeDemoAnonXlat
-- Azure SQL note: connect directly to PluralBridgeDemoAnonXlat before running. No USE statement.

SET NOCOUNT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. Connect directly to PluralBridgeDemoAnonXlat before running this script.', 1;
END;

CREATE TABLE #Patterns
(
    PatternLabel nvarchar(100) NOT NULL,
    SqlLikePattern nvarchar(4000) NOT NULL
);

INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'email_at_sign', N'%@%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'http_url', N'%http://%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'https_url', N'%https://%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'bearer_token_word', N'%bearer%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'token_word', N'%token%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'password_word', N'%password%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'secret_word', N'%secret%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'account_word', N'%account%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'avatar_word', N'%avatar%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'note_word', N'%note%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'friend_word', N'%friend%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'simply_plural_name', N'%simply plural%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'simplyplural_name', N'%simplyplural%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'apparyllis_name', N'%apparyllis%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'private_source_db_name', N'%PluralBridgeCloudProof001%');
INSERT INTO #Patterns (PatternLabel, SqlLikePattern) VALUES (N'old_demo_db_name', N'%PluralBridgeDemoAnon001%');

CREATE TABLE #ResidueHits
(
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    PatternLabel nvarchar(100) NOT NULL,
    HitCount int NOT NULL,
    SampleValue nvarchar(4000) NULL
);

DECLARE @TableName sysname;
DECLARE @ColumnName sysname;
DECLARE @Sql nvarchar(max);

DECLARE column_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) AS TableName,
    QUOTENAME(c.name) AS ColumnName
FROM sys.tables AS t
JOIN sys.columns AS c
    ON c.object_id = t.object_id
JOIN sys.types AS ty
    ON ty.user_type_id = c.user_type_id
WHERE
    SCHEMA_NAME(t.schema_id) = N'dbo'
    AND t.name LIKE N'pb[_]%'
    AND ty.name IN (N'char', N'varchar', N'nchar', N'nvarchar', N'text', N'ntext', N'xml')
ORDER BY t.name, c.column_id;

OPEN column_cursor;
FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'
        INSERT INTO #ResidueHits
        (
            TableName,
            ColumnName,
            PatternLabel,
            HitCount,
            SampleValue
        )
        SELECT
            @TableNameForOutput,
            @ColumnNameForOutput,
            p.PatternLabel,
            COUNT(*) AS HitCount,
            MIN(CONVERT(nvarchar(4000), src.' + @ColumnName + N')) AS SampleValue
        FROM ' + @TableName + N' AS src
        CROSS JOIN #Patterns AS p
        WHERE
            src.' + @ColumnName + N' IS NOT NULL
            AND CONVERT(nvarchar(max), src.' + @ColumnName + N') COLLATE Latin1_General_CI_AS LIKE p.SqlLikePattern COLLATE Latin1_General_CI_AS
        GROUP BY p.PatternLabel
        HAVING COUNT(*) > 0;';

    EXEC sp_executesql
        @Sql,
        N'@TableNameForOutput sysname, @ColumnNameForOutput sysname',
        @TableNameForOutput = @TableName,
        @ColumnNameForOutput = @ColumnName;

    FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName;
END

CLOSE column_cursor;
DEALLOCATE column_cursor;

SELECT
    TableName,
    ColumnName,
    PatternLabel,
    HitCount,
    SampleValue
FROM #ResidueHits
ORDER BY TableName, ColumnName, PatternLabel;

SELECT
    CASE
        WHEN EXISTS (SELECT 1 FROM #ResidueHits)
            THEN 'REVIEW: possible private-data residue was found.'
        ELSE 'PASS: no configured text residue patterns were found.'
    END AS validation_result,
    COUNT(*) AS residue_hit_groups
FROM #ResidueHits;

DROP TABLE #ResidueHits;
DROP TABLE #Patterns;
