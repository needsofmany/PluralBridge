-- Avatar GET extended-events capture, start.
-- Execution order, all in SSMS against PluralBridgeChap2SafeSpine:
--   1. Rebuild and restart the API so AvatarGet includes ClientConnectionId.
--   2. Run this script. Leave this query window open.
--   3. Load one Members avatar pass, without other API traffic if possible.
--   4. In this same window, run 02_read_capture.sql.
--   5. Run 03_cleanup_capture.sql. That script may use a different window.
-- This script creates no tables, writes no application data, and does not
-- alter connection settings. The ring buffer lives in memory for this
-- database-scoped session only.
-- The client filter is the Microsoft.Data.SqlClient 7.0.1 default,
-- N'Core .Net SqlClient Data Provider'. PluralBridge.Api constructs
-- SqlConnection from its configured connection string and does not set
-- ApplicationName. That connection string has no Application Name keyword,
-- so the driver default is the name sent to SQL Server.
-- Statement-level events and execution-plan events are not added.
-- RPC parameter and data-stream collection is not enabled.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName sysname = N'PluralBridgeChap2SafeSpine';
DECLARE @SessionName sysname = N'pb_avatar_profile_capture';
--- changed
DECLARE @ClientAppName nvarchar(128) = N'Core Microsoft SqlClient Data Provider';
---
DECLARE @RingTargetMaxMemoryKb int = 1024;
DECLARE @SessionMaxMemoryKb int = 4096;
DECLARE @MaxEventsLimit int = 2000;
DECLARE @AppLiteral nvarchar(256) = REPLACE(@ClientAppName, N'''', N'''''');
DECLARE @AttentionIncluded bit = 0;
DECLARE @HasSqlText bit = 0;
DECLARE @HasSystemTime bit = 0;
DECLARE @HasMaxEvents bit = 0;
DECLARE @ErrorHasSeverity bit = 0;
DECLARE @ActionList nvarchar(max);
DECLARE @TargetOptions nvarchar(max);
DECLARE @CreateSql nvarchar(max);
DECLARE @SessionCreated bit = 0;
DECLARE @CaptureStartedAtUtc datetime2(7);

DECLARE @Missing TABLE
(
    ObjectKind nvarchar(40) NOT NULL,
    PackageName sysname NOT NULL,
    ObjectName sysname NOT NULL
);

DECLARE @IncludeEvent TABLE
(
    EventName sysname NOT NULL PRIMARY KEY,
    SortOrder int NOT NULL
);

DECLARE @DisableColumn TABLE
(
    EventName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    PRIMARY KEY (EventName, ColumnName)
);

DECLARE @Action TABLE
(
    SortOrder int NOT NULL PRIMARY KEY,
    ActionText nvarchar(256) NOT NULL
);

DECLARE @EventSql TABLE
(
    SortOrder int NOT NULL PRIMARY KEY,
    Ddl nvarchar(max) NOT NULL
);

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> @DatabaseName
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

IF EXISTS
(
    SELECT 1
    FROM sys.database_event_sessions
    WHERE name = @SessionName
)
    THROW 50000, 'Stop: event session pb_avatar_profile_capture already exists. Run 03_cleanup_capture.sql. This script does not overwrite it.', 1;

INSERT INTO @Missing (ObjectKind, PackageName, ObjectName)
SELECT N'event', N'sqlserver', required.EventName
FROM
(
    VALUES
        (N'rpc_starting'),
        (N'rpc_completed'),
        (N'sql_batch_starting'),
        (N'sql_batch_completed'),
        (N'error_reported')
) AS required(EventName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM sys.dm_xe_objects AS xe_object
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_object.package_guid
    WHERE xe_object.object_type = N'event'
      AND xe_package.name = N'sqlserver'
      AND xe_object.name = required.EventName
);

INSERT INTO @Missing (ObjectKind, PackageName, ObjectName)
SELECT N'action', required.PackageName, required.ActionName
FROM
(
    VALUES
        (N'sqlserver', N'client_app_name'),
        (N'sqlserver', N'client_connection_id'),
        (N'sqlserver', N'session_id')
) AS required(PackageName, ActionName)
WHERE NOT EXISTS
(
    SELECT 1
    FROM sys.dm_xe_objects AS xe_object
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_object.package_guid
    WHERE xe_object.object_type = N'action'
      AND xe_package.name = required.PackageName
      AND xe_object.name = required.ActionName
);

INSERT INTO @Missing (ObjectKind, PackageName, ObjectName)
SELECT N'target', N'package0', N'ring_buffer'
WHERE NOT EXISTS
(
    SELECT 1
    FROM sys.dm_xe_objects AS xe_object
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_object.package_guid
    WHERE xe_object.object_type = N'target'
      AND xe_package.name = N'package0'
      AND xe_object.name = N'ring_buffer'
);

INSERT INTO @Missing (ObjectKind, PackageName, ObjectName)
SELECT N'target-option', N'package0', N'max_memory'
WHERE NOT EXISTS
(
    SELECT 1
    FROM sys.dm_xe_object_columns AS xe_column
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_column.object_package_guid
    WHERE xe_column.object_name = N'ring_buffer'
      AND xe_package.name = N'package0'
      AND xe_column.name = N'max_memory'
);

INSERT INTO @Missing (ObjectKind, PackageName, ObjectName)
SELECT N'uncontrolled-payload', N'sqlserver', xe_object.name + N'.' + xe_column.name
FROM sys.dm_xe_objects AS xe_object
INNER JOIN sys.dm_xe_packages AS xe_package
    ON xe_package.guid = xe_object.package_guid
INNER JOIN sys.dm_xe_object_columns AS xe_column
    ON xe_column.object_name = xe_object.name
   AND xe_column.object_package_guid = xe_object.package_guid
WHERE xe_object.object_type = N'event'
  AND xe_package.name = N'sqlserver'
  AND xe_object.name IN
  (
      N'rpc_starting',
      N'rpc_completed',
      N'sql_batch_starting',
      N'sql_batch_completed',
      N'error_reported',
      N'attention'
  )
  AND xe_column.name IN (N'data_stream', N'output_parameters')
  AND NOT EXISTS
  (
      SELECT 1
      FROM sys.dm_xe_object_columns AS switch_column
      WHERE switch_column.object_name = xe_object.name
        AND switch_column.object_package_guid = xe_object.package_guid
        AND switch_column.name = CASE xe_column.name
            WHEN N'data_stream' THEN N'collect_data_stream'
            ELSE N'collect_output_parameters'
        END
        AND
        (
            switch_column.column_type = N'customizable'
            OR switch_column.capabilities_desc LIKE N'%customizable%'
        )
  );

IF EXISTS (SELECT 1 FROM @Missing)
BEGIN
    SELECT ObjectKind, PackageName, ObjectName
    FROM @Missing
    ORDER BY ObjectKind, PackageName, ObjectName;

    THROW 50000, 'Stop: Extended Events metadata is missing a required object, or an event would expose a payload this script cannot turn off. No session was created.', 1;
END;

SELECT @AttentionIncluded = CASE WHEN EXISTS
(
    SELECT 1
    FROM sys.dm_xe_objects AS xe_object
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_object.package_guid
    WHERE xe_object.object_type = N'event'
      AND xe_package.name = N'sqlserver'
      AND xe_object.name = N'attention'
) THEN 1 ELSE 0 END;

SELECT @HasSqlText = CASE WHEN EXISTS
(
    SELECT 1
    FROM sys.dm_xe_objects AS xe_object
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_object.package_guid
    WHERE xe_object.object_type = N'action'
      AND xe_package.name = N'sqlserver'
      AND xe_object.name = N'sql_text'
) THEN 1 ELSE 0 END;

SELECT @HasSystemTime = CASE WHEN EXISTS
(
    SELECT 1
    FROM sys.dm_xe_objects AS xe_object
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_object.package_guid
    WHERE xe_object.object_type = N'action'
      AND xe_package.name = N'package0'
      AND xe_object.name = N'collect_system_time'
) THEN 1 ELSE 0 END;

SELECT @HasMaxEvents = CASE WHEN EXISTS
(
    SELECT 1
    FROM sys.dm_xe_object_columns AS xe_column
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_column.object_package_guid
    WHERE xe_column.object_name = N'ring_buffer'
      AND xe_package.name = N'package0'
      AND xe_column.name = N'max_events_limit'
) THEN 1 ELSE 0 END;

SELECT @ErrorHasSeverity = CASE WHEN EXISTS
(
    SELECT 1
    FROM sys.dm_xe_object_columns AS xe_column
    INNER JOIN sys.dm_xe_packages AS xe_package
        ON xe_package.guid = xe_column.object_package_guid
    WHERE xe_column.object_name = N'error_reported'
      AND xe_package.name = N'sqlserver'
      AND xe_column.name = N'severity'
) THEN 1 ELSE 0 END;

INSERT INTO @IncludeEvent (EventName, SortOrder)
VALUES
    (N'error_reported', 10),
    (N'rpc_completed', 20),
    (N'rpc_starting', 30),
    (N'sql_batch_completed', 40),
    (N'sql_batch_starting', 50);

IF @AttentionIncluded = 1
    INSERT INTO @IncludeEvent (EventName, SortOrder)
    VALUES (N'attention', 60);

INSERT INTO @DisableColumn (EventName, ColumnName)
SELECT xe_object.name, xe_column.name
FROM sys.dm_xe_objects AS xe_object
INNER JOIN sys.dm_xe_packages AS xe_package
    ON xe_package.guid = xe_object.package_guid
INNER JOIN sys.dm_xe_object_columns AS xe_column
    ON xe_column.object_name = xe_object.name
   AND xe_column.object_package_guid = xe_object.package_guid
INNER JOIN @IncludeEvent AS included
    ON included.EventName = xe_object.name
WHERE xe_object.object_type = N'event'
  AND xe_package.name = N'sqlserver'
  AND xe_column.name IN (N'collect_data_stream', N'collect_output_parameters')
  AND
  (
      xe_column.column_type = N'customizable'
      OR xe_column.capabilities_desc LIKE N'%customizable%'
  );

INSERT INTO @Action (SortOrder, ActionText)
VALUES
    (1, N'sqlserver.client_app_name'),
    (2, N'sqlserver.client_connection_id'),
    (3, N'sqlserver.session_id');

IF @HasSqlText = 1
    INSERT INTO @Action (SortOrder, ActionText)
    VALUES (4, N'sqlserver.sql_text');

IF @HasSystemTime = 1
    INSERT INTO @Action (SortOrder, ActionText)
    VALUES (5, N'package0.collect_system_time');

SELECT @ActionList = STRING_AGG(ActionText, N', ') WITHIN GROUP (ORDER BY SortOrder)
FROM @Action;

INSERT INTO @EventSql (SortOrder, Ddl)
SELECT
    included.SortOrder,
    N'ADD EVENT sqlserver.' + QUOTENAME(included.EventName) + N'('
        + CASE
            WHEN set_clause.SetClause IS NULL THEN N''
            ELSE N'SET ' + set_clause.SetClause + N' '
          END
        + N'ACTION (' + @ActionList + N') '
        + N'WHERE (sqlserver.client_app_name = N''' + @AppLiteral + N''''
        + CASE
            WHEN included.EventName = N'error_reported' AND @ErrorHasSeverity = 1 THEN N' AND (severity >= 11)'
            ELSE N''
          END
        + N'))'
FROM @IncludeEvent AS included
OUTER APPLY
(
    SELECT STRING_AGG(
            CASE disabled.ColumnName
                WHEN N'collect_data_stream' THEN N'collect_data_stream=(0)'
                WHEN N'collect_output_parameters' THEN N'collect_output_parameters=(0)'
            END,
            N', ')
        WITHIN GROUP (ORDER BY CASE disabled.ColumnName
            WHEN N'collect_data_stream' THEN 1
            WHEN N'collect_output_parameters' THEN 2
            ELSE 3
        END) AS SetClause
    FROM @DisableColumn AS disabled
    WHERE disabled.EventName = included.EventName
) AS set_clause;

SELECT @CreateSql = STRING_AGG(Ddl, N',' + NCHAR(10)) WITHIN GROUP (ORDER BY SortOrder)
FROM @EventSql;

SET @CreateSql =
    N'CREATE EVENT SESSION ' + QUOTENAME(@SessionName) + N' ON DATABASE ' + NCHAR(10)
    + @CreateSql + NCHAR(10);

SET @TargetOptions = N'max_memory = (' + CONVERT(nvarchar(11), @RingTargetMaxMemoryKb) + N')';
IF @HasMaxEvents = 1
    SET @TargetOptions = @TargetOptions + N', max_events_limit = (' + CONVERT(nvarchar(11), @MaxEventsLimit) + N')';

SET @CreateSql = @CreateSql
    + N'ADD TARGET package0.ring_buffer(SET ' + @TargetOptions + N')' + NCHAR(10)
    + N'WITH (' + NCHAR(10)
    + N'    MAX_MEMORY = ' + CONVERT(nvarchar(11), @SessionMaxMemoryKb) + N' KB,' + NCHAR(10)
    + N'    EVENT_RETENTION_MODE = ALLOW_SINGLE_EVENT_LOSS,' + NCHAR(10)
    + N'    MAX_DISPATCH_LATENCY = 1 SECONDS,' + NCHAR(10)
    + N'    TRACK_CAUSALITY = OFF,' + NCHAR(10)
    + N'    STARTUP_STATE = OFF' + NCHAR(10)
    + N');';

SELECT CAST(N'<definition><![CDATA[' + REPLACE(@CreateSql, N']]>', N']]]]><![CDATA[>') + N']]></definition>' AS xml) AS SessionDefinition;

BEGIN TRY
    EXEC sys.sp_executesql @CreateSql;
    SET @SessionCreated = 1;

    EXEC sys.sp_executesql
        N'ALTER EVENT SESSION [pb_avatar_profile_capture] ON DATABASE STATE = START;';

    SET @CaptureStartedAtUtc = SYSUTCDATETIME();

    DROP TABLE IF EXISTS #AvatarProfileWaitBaseline;
    DROP TABLE IF EXISTS #AvatarProfileCapture;
    DROP TABLE IF EXISTS #AvatarProfileConfiguredEvent;
    DROP TABLE IF EXISTS #AvatarProfileOption;

    SELECT
        wait_type,
        waiting_tasks_count,
        wait_time_ms,
        signal_wait_time_ms
    INTO #AvatarProfileWaitBaseline
    FROM sys.dm_db_wait_stats;

    CREATE TABLE #AvatarProfileCapture
    (
        CaptureStartedAtUtc datetime2(7) NOT NULL,
        SessionName sysname NOT NULL,
        ClientAppName nvarchar(128) NOT NULL
    );

    INSERT INTO #AvatarProfileCapture (CaptureStartedAtUtc, SessionName, ClientAppName)
    VALUES (@CaptureStartedAtUtc, @SessionName, @ClientAppName);

    CREATE TABLE #AvatarProfileConfiguredEvent
    (
        EventName sysname NOT NULL
    );

    INSERT INTO #AvatarProfileConfiguredEvent (EventName)
    SELECT EventName
    FROM @IncludeEvent;

    CREATE TABLE #AvatarProfileOption
    (
        OptionName nvarchar(128) NOT NULL,
        OptionValue nvarchar(128) NOT NULL
    );

    INSERT INTO #AvatarProfileOption (OptionName, OptionValue)
    VALUES
        (N'ClientAppName', @ClientAppName),
        (N'AttentionEvent', CASE WHEN @AttentionIncluded = 1 THEN N'included' ELSE N'not supported on this engine' END),
        (N'SqlTextAction', CASE WHEN @HasSqlText = 1 THEN N'included' ELSE N'not supported on this engine' END),
        (N'CollectSystemTime', CASE WHEN @HasSystemTime = 1 THEN N'included' ELSE N'not supported; event timestamp is still UTC' END),
        (N'RingTargetMaxMemoryKb', CONVERT(nvarchar(11), @RingTargetMaxMemoryKb)),
        (N'SessionMaxMemoryKb', CONVERT(nvarchar(11), @SessionMaxMemoryKb)),
        (N'MaxEventsLimit', CASE WHEN @HasMaxEvents = 1 THEN CONVERT(nvarchar(11), @MaxEventsLimit) ELSE N'not supported on this target' END),
        (N'EventRetention', N'ALLOW_SINGLE_EVENT_LOSS'),
        (N'ErrorSeverityFilter', CASE WHEN @ErrorHasSeverity = 1 THEN N'severity >= 11' ELSE N'not applied; severity column is absent' END);

    INSERT INTO #AvatarProfileOption (OptionName, OptionValue)
    SELECT
        N'Disabled.' + EventName + N'.' + ColumnName,
        N'0'
    FROM @DisableColumn;

    SELECT
        CaptureStartedAtUtc,
        SessionName,
        ClientAppName,
        N'started' AS SessionState
    FROM #AvatarProfileCapture;

    SELECT EventName
    FROM #AvatarProfileConfiguredEvent
    ORDER BY EventName;

    SELECT OptionName, OptionValue
    FROM #AvatarProfileOption
    ORDER BY OptionName;

    SELECT
        N'Leave this window open. Load one avatar pass now, then run 02_read_capture.sql here.' AS NextStep;
END TRY
BEGIN CATCH
    IF @SessionCreated = 1
    BEGIN
        BEGIN TRY
            EXEC sys.sp_executesql
                N'ALTER EVENT SESSION [pb_avatar_profile_capture] ON DATABASE STATE = STOP;';
        END TRY
        BEGIN CATCH
            -- The original failure is rethrown below. A session that never started has nothing to stop.
        END CATCH;

        BEGIN TRY
            EXEC sys.sp_executesql
                N'DROP EVENT SESSION [pb_avatar_profile_capture] ON DATABASE;';
        END TRY
        BEGIN CATCH
            -- Keep the original error. 03_cleanup_capture.sql can drop a session that remains.
        END CATCH;
    END;

    THROW;
END CATCH;
