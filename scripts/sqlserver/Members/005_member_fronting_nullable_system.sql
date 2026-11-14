-- One open fronting interval per member.
-- This script has not been executed.
-- Install after 001 through 003. It does not depend on 004.
-- It keeps FK_pb_front_history_pb_systems and allows SystemId NULL
-- so a systemless member can have an interval. It does not rewrite
-- existing fronting rows. It stops if a member already has more than
-- one open interval.
-- Preflight checks and schema changes run in one transaction. A failed
-- check rolls that transaction back and makes no schema change.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @ScriptTransaction bit = 0;

BEGIN TRY
    IF @@TRANCOUNT <> 0
        THROW 50000, 'Stop: this script must not run inside an existing transaction.', 1;

    BEGIN TRANSACTION;
    SET @ScriptTransaction = 1;

    IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
        THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

    IF COL_LENGTH(N'dbo.pb_front_history', N'SystemId') IS NULL
        THROW 50000, 'Stop: dbo.pb_front_history.SystemId was not found.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.pb_front_history')
          AND name = N'SystemId'
          AND is_nullable = 1
    )
    OR EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.pb_front_history')
          AND name = N'UQ_pb_front_history_OneOpenInterval'
    )
        THROW 50000, 'Stop: member fronting storage is already installed.', 1;

    DECLARE @OpenConflicts TABLE
    (
        MemberId uniqueidentifier NOT NULL,
        OpenIntervalCount int NOT NULL
    );

    INSERT INTO @OpenConflicts (MemberId, OpenIntervalCount)
    SELECT
        MemberId,
        COUNT(*)
    FROM dbo.pb_front_history WITH (UPDLOCK, HOLDLOCK, TABLOCKX)
    WHERE EndTimeMs IS NULL
      AND IsLive = 1
    GROUP BY MemberId
    HAVING COUNT(*) > 1;

    IF EXISTS (SELECT 1 FROM @OpenConflicts)
    BEGIN
        SELECT
            MemberId,
            OpenIntervalCount
        FROM @OpenConflicts
        ORDER BY MemberId;

        THROW 50000, 'Stop: a member already has more than one open fronting interval. The result lists each conflicting MemberId and OpenIntervalCount. Resolve those rows before installing this script.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_pb_front_history_pb_systems'
          AND parent_object_id = OBJECT_ID(N'dbo.pb_front_history')
    )
    BEGIN
        ALTER TABLE dbo.pb_front_history
            DROP CONSTRAINT FK_pb_front_history_pb_systems;
    END;

    ALTER TABLE dbo.pb_front_history
        ALTER COLUMN SystemId uniqueidentifier NULL;

    ALTER TABLE dbo.pb_front_history WITH CHECK
        ADD CONSTRAINT FK_pb_front_history_pb_systems
        FOREIGN KEY (SystemId) REFERENCES dbo.pb_systems (SystemId);

    ALTER TABLE dbo.pb_front_history
        CHECK CONSTRAINT FK_pb_front_history_pb_systems;

    CREATE UNIQUE INDEX UQ_pb_front_history_OneOpenInterval
        ON dbo.pb_front_history (MemberId)
        WHERE EndTimeMs IS NULL AND IsLive = 1;

    COMMIT TRANSACTION;
    SET @ScriptTransaction = 0;
END TRY
BEGIN CATCH
    IF @ScriptTransaction = 1 AND @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT DB_NAME() AS CurrentDatabase;

SELECT
    c.name AS ColumnName,
    c.is_nullable,
    fk.name AS ForeignKeyName
FROM sys.columns AS c
LEFT JOIN sys.foreign_key_columns AS fkc
    ON fkc.parent_object_id = c.object_id
   AND fkc.parent_column_id = c.column_id
LEFT JOIN sys.foreign_keys AS fk
    ON fk.object_id = fkc.constraint_object_id
   AND fk.name = N'FK_pb_front_history_pb_systems'
WHERE c.object_id = OBJECT_ID(N'dbo.pb_front_history')
  AND c.name = N'SystemId';

SELECT
    name AS IndexName,
    is_unique,
    has_filter,
    filter_definition
FROM sys.indexes
WHERE object_id = OBJECT_ID(N'dbo.pb_front_history')
  AND name = N'UQ_pb_front_history_OneOpenInterval';
