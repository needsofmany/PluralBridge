-- 006_clear_source_records_rawjson.sql
-- Purpose: Replace every dbo.pb_source_records.RawJson value with the safe empty JSON array text: [ ]
-- Target database: PluralBridgeDemoAnonXlat
-- Azure SQL note: connect directly to PluralBridgeDemoAnonXlat before running. No USE statement.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. Connect directly to PluralBridgeDemoAnonXlat before running this script.', 1;
END;

IF OBJECT_ID(N'dbo.pb_source_records', N'U') IS NULL
BEGIN
    THROW 51001, 'Expected table dbo.pb_source_records was not found.', 1;
END;

IF COL_LENGTH(N'dbo.pb_source_records', N'RawJson') IS NULL
BEGIN
    THROW 51002, 'Expected column dbo.pb_source_records.RawJson was not found.', 1;
END;

IF (SELECT COUNT(*) FROM dbo.pb_source_records) <> 945
BEGIN
    THROW 51003, 'Expected 945 rows in dbo.pb_source_records before clearing RawJson.', 1;
END;

DECLARE @RowsBefore int;
SELECT @RowsBefore = COUNT(*)
FROM dbo.pb_source_records;

BEGIN TRANSACTION;

UPDATE dbo.pb_source_records
SET RawJson = N'[ ]';

DECLARE @RowsUpdated int = @@ROWCOUNT;

IF @RowsUpdated <> @RowsBefore
BEGIN
    THROW 51004, 'RawJson cleanup did not update every pb_source_records row.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_source_records
    WHERE RawJson IS NULL
       OR RawJson <> N'[ ]'
)
BEGIN
    THROW 51005, 'RawJson cleanup verification failed. At least one row is not [ ].', 1;
END;

COMMIT TRANSACTION;

SELECT
    'PASS: every pb_source_records.RawJson value was set to [ ].' AS validation_result,
    COUNT(*) AS pb_source_records,
    SUM(CASE WHEN RawJson = N'[ ]' THEN 1 ELSE 0 END) AS rows_with_safe_rawjson
FROM dbo.pb_source_records;
