-- Avatar GET extended-events capture, read.
-- Run this in the same SSMS window as 01_start_capture.sql, after one avatar load.
-- The first batch copies the ring buffer and the wait counters, then stops the
-- session. The second batch reads those copies. It does not drop the session
-- and does not write application data.
-- Save the result grids. Statement text in the main grid is limited to 4000
-- characters, and a separate result returns anything longer as XML so the grid
-- does not silently cut it off. The raw ring-buffer XML stays in
-- #AvatarProfileRingBuffer in this window and is not dumped to the grid.
-- Do not add DurationMicroseconds across rows. Each value belongs to that event
-- alone. A batch and the RPCs inside it are separate events, not a sum.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName sysname = N'PluralBridgeChap2SafeSpine';
DECLARE @SessionName sysname = N'pb_avatar_profile_capture';
DECLARE @SnapshotStartedAtUtc datetime2(7);
DECLARE @TargetXml xml;
DECLARE @SessionDropped bigint;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> @DatabaseName
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

IF OBJECT_ID(N'tempdb..#AvatarProfileCapture') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileWaitBaseline') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileConfiguredEvent') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileOption') IS NULL
    THROW 50000, 'Stop: run 01_start_capture.sql in this same SSMS window first. Its temporary tables do not survive a new window.', 1;

IF OBJECT_ID(N'tempdb..#AvatarProfileRingBuffer') IS NOT NULL
    AND
    (
        OBJECT_ID(N'tempdb..#AvatarProfileWaitEnd') IS NULL
        OR OBJECT_ID(N'tempdb..#AvatarProfileSessionDropped') IS NULL
    )
    THROW 50000, 'Stop: this window has a partial snapshot. Close it, run 03_cleanup_capture.sql, then start again.', 1;

IF OBJECT_ID(N'tempdb..#AvatarProfileRingBuffer') IS NULL
BEGIN
    BEGIN TRY
        IF NOT EXISTS
        (
            SELECT 1
            FROM sys.dm_xe_database_sessions
            WHERE name = @SessionName
        )
            THROW 50000, 'Stop: pb_avatar_profile_capture is not running in this database, and this window has no ring-buffer snapshot.', 1;

        IF
        (
            SELECT COUNT(*)
            FROM sys.dm_xe_database_session_targets AS target
            INNER JOIN sys.dm_xe_database_sessions AS session_row
                ON session_row.address = target.event_session_address
            WHERE session_row.name = @SessionName
              AND target.target_name = N'ring_buffer'
        ) <> 1
            THROW 50000, 'Stop: expected one ring-buffer target on pb_avatar_profile_capture. The session was left running.', 1;

        SELECT @TargetXml = CAST(target.target_data AS xml)
        FROM sys.dm_xe_database_session_targets AS target
        INNER JOIN sys.dm_xe_database_sessions AS session_row
            ON session_row.address = target.event_session_address
        WHERE session_row.name = @SessionName
          AND target.target_name = N'ring_buffer';

        IF @TargetXml IS NULL
            THROW 50000, 'Stop: the ring-buffer target returned no XML. The session was left running.', 1;

        SET @SnapshotStartedAtUtc = SYSUTCDATETIME();

        CREATE TABLE #AvatarProfileRingBuffer
        (
            TargetXml xml NOT NULL,
            SnapshotStartedAtUtc datetime2(7) NOT NULL
        );

        INSERT INTO #AvatarProfileRingBuffer (TargetXml, SnapshotStartedAtUtc)
        VALUES (@TargetXml, @SnapshotStartedAtUtc);

        SELECT
            wait_type,
            waiting_tasks_count,
            wait_time_ms,
            signal_wait_time_ms,
            @SnapshotStartedAtUtc AS ReadAtUtc
        INTO #AvatarProfileWaitEnd
        FROM sys.dm_db_wait_stats;

        BEGIN TRY
            SELECT @SessionDropped = dropped_event_count
            FROM sys.dm_xe_database_sessions
            WHERE name = @SessionName;
        END TRY
        BEGIN CATCH
            SET @SessionDropped = NULL;
        END CATCH;

        CREATE TABLE #AvatarProfileSessionDropped
        (
            SessionDroppedEventCount bigint NULL
        );

        INSERT INTO #AvatarProfileSessionDropped (SessionDroppedEventCount)
        VALUES (@SessionDropped);
    END TRY
    BEGIN CATCH
        DROP TABLE IF EXISTS #AvatarProfileRingBuffer;
        DROP TABLE IF EXISTS #AvatarProfileWaitEnd;
        DROP TABLE IF EXISTS #AvatarProfileSessionDropped;
        THROW;
    END CATCH;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.dm_xe_database_sessions
    WHERE name = @SessionName
)
    ALTER EVENT SESSION [pb_avatar_profile_capture] ON DATABASE STATE = STOP;

GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName sysname = N'PluralBridgeChap2SafeSpine';
DECLARE @CaptureStartedAtUtc datetime2(7);
DECLARE @SnapshotStartedAtUtc datetime2(7);
DECLARE @TargetXml xml;
DECLARE @DroppedCount bigint;
DECLARE @Truncated int;
DECLARE @EventCount bigint;
DECLARE @TotalProcessed bigint;
DECLARE @EventsNotRetained bigint;
DECLARE @SessionDropped bigint;
DECLARE @ShreddedCount int;

IF DB_NAME() <> @DatabaseName
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

IF OBJECT_ID(N'tempdb..#AvatarProfileCapture') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileRingBuffer') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileWaitBaseline') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileWaitEnd') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileSessionDropped') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileConfiguredEvent') IS NULL
    OR OBJECT_ID(N'tempdb..#AvatarProfileOption') IS NULL
    THROW 50000, 'Stop: the snapshot temporary tables are missing. Run 01_start_capture.sql and the first batch of this script in this same window.', 1;

SELECT @CaptureStartedAtUtc = CaptureStartedAtUtc
FROM #AvatarProfileCapture;

SELECT
    @TargetXml = TargetXml,
    @SnapshotStartedAtUtc = SnapshotStartedAtUtc
FROM #AvatarProfileRingBuffer;

SELECT @SessionDropped = SessionDroppedEventCount
FROM #AvatarProfileSessionDropped;

SET @DroppedCount = @TargetXml.value(N'(/RingBufferTarget/@droppedCount)[1]', N'bigint');
SET @Truncated = @TargetXml.value(N'(/RingBufferTarget/@truncated)[1]', N'int');
SET @EventCount = @TargetXml.value(N'(/RingBufferTarget/@eventCount)[1]', N'bigint');
SET @TotalProcessed = @TargetXml.value(N'(/RingBufferTarget/@totalEventsProcessed)[1]', N'bigint');
SET @EventsNotRetained = CASE
    WHEN @TotalProcessed IS NULL OR @EventCount IS NULL THEN NULL
    WHEN @TotalProcessed > @EventCount THEN @TotalProcessed - @EventCount
    ELSE 0
END;

DROP TABLE IF EXISTS #AvatarProfileEvents;

CREATE TABLE #AvatarProfileEvents
(
    EventSequence int NOT NULL IDENTITY(1, 1),
    EventName nvarchar(128) NULL,
    EventAtUtc datetime2(7) NULL,
    EventAtUtcText nvarchar(40) NULL,
    ClientConnectionId uniqueidentifier NULL,
    SessionId int NULL,
    ClientAppName nvarchar(128) NULL,
    DurationMicroseconds bigint NULL,
    CpuMicroseconds bigint NULL,
    LogicalReads bigint NULL,
    PhysicalReads bigint NULL,
    Writes bigint NULL,
    ResultRows bigint NULL,
    RpcResult nvarchar(32) NULL,
    ObjectName nvarchar(256) NULL,
    ErrorNumber int NULL,
    Severity int NULL,
    ErrorState int NULL,
    SqlText nvarchar(max) NULL,
    ErrorMessage nvarchar(max) NULL
);

INSERT INTO #AvatarProfileEvents
(
    EventName,
    EventAtUtc,
    EventAtUtcText,
    ClientConnectionId,
    SessionId,
    ClientAppName,
    DurationMicroseconds,
    CpuMicroseconds,
    LogicalReads,
    PhysicalReads,
    Writes,
    ResultRows,
    RpcResult,
    ObjectName,
    ErrorNumber,
    Severity,
    ErrorState,
    SqlText,
    ErrorMessage
)
SELECT
    event_node.value(N'@name', N'nvarchar(128)'),
    TRY_CONVERT(datetime2(7), event_node.value(N'@timestamp', N'nvarchar(40)'), 127),
    event_node.value(N'@timestamp', N'nvarchar(40)'),
    TRY_CONVERT(uniqueidentifier, event_node.value(N'(action[@name="client_connection_id"]/value/text())[1]', N'nvarchar(40)')),
    TRY_CONVERT(int, event_node.value(N'(action[@name="session_id"]/value/text())[1]', N'nvarchar(20)')),
    event_node.value(N'(action[@name="client_app_name"]/value/text())[1]', N'nvarchar(128)'),
    TRY_CONVERT(bigint, event_node.value(N'(data[@name="duration"]/value/text())[1]', N'nvarchar(30)')),
    TRY_CONVERT(bigint, event_node.value(N'(data[@name="cpu_time"]/value/text())[1]', N'nvarchar(30)')),
    TRY_CONVERT(bigint, event_node.value(N'(data[@name="logical_reads"]/value/text())[1]', N'nvarchar(30)')),
    TRY_CONVERT(bigint, event_node.value(N'(data[@name="physical_reads"]/value/text())[1]', N'nvarchar(30)')),
    TRY_CONVERT(bigint, event_node.value(N'(data[@name="writes"]/value/text())[1]', N'nvarchar(30)')),
    TRY_CONVERT(bigint, event_node.value(N'(data[@name="row_count"]/value/text())[1]', N'nvarchar(30)')),
    event_node.value(N'(data[@name="result"]/value/text())[1]', N'nvarchar(32)'),
    event_node.value(N'(data[@name="object_name"]/value/text())[1]', N'nvarchar(256)'),
    TRY_CONVERT(int, event_node.value(N'(data[@name="error_number"]/value/text())[1]', N'nvarchar(20)')),
    TRY_CONVERT(int, event_node.value(N'(data[@name="severity"]/value/text())[1]', N'nvarchar(20)')),
    TRY_CONVERT(int, event_node.value(N'(data[@name="state"]/value/text())[1]', N'nvarchar(20)')),
    COALESCE(
        event_node.value(N'(action[@name="sql_text"]/value/text())[1]', N'nvarchar(max)'),
        event_node.value(N'(data[@name="statement"]/value/text())[1]', N'nvarchar(max)'),
        event_node.value(N'(data[@name="batch_text"]/value/text())[1]', N'nvarchar(max)')),
    event_node.value(N'(data[@name="message"]/value/text())[1]', N'nvarchar(max)')
FROM @TargetXml.nodes(N'/RingBufferTarget/event') AS events(event_node);

SELECT @ShreddedCount = COUNT(*)
FROM #AvatarProfileEvents;

SELECT
    Metric,
    Unit,
    Meaning
FROM
(
    VALUES
        (N'EventAtUtc', N'UTC', N'Extended Events timestamp of this event. It is not a duration.'),
        (N'DurationMicroseconds', N'microseconds', N'This event alone. Do not add durations across rows.'),
        (N'CpuMicroseconds', N'microseconds', N'CPU time reported by this event alone.'),
        (N'LogicalReads', N'8 KB pages', N'Logical reads reported by this event.'),
        (N'PhysicalReads', N'8 KB pages', N'Physical reads reported by this event.'),
        (N'Writes', N'8 KB pages', N'Writes reported by this event.'),
        (N'ResultRows', N'rows', N'Row count reported by this event.'),
        (N'AvgCpuPercentOfLimit', N'percent of database CPU limit', N'15-second average. Not CPU time and not a request total.'),
        (N'AvgDataIoPercentOfLimit', N'percent of database data I/O limit', N'15-second average. Not bytes.'),
        (N'AvgLogWritePercentOfLimit', N'percent of database log-write limit', N'15-second average. Not bytes.'),
        (N'MaxWorkerPercentOfLimit', N'percent of worker limit', N'Highest worker use in that 15-second interval.'),
        (N'MaxSessionPercentOfLimit', N'percent of session limit', N'Highest session use in that 15-second interval.'),
        (N'DtuLimit', N'DTUs', N'The database DTU limit during that interval.'),
        (N'WaitTimeMillisecondsDelta', N'milliseconds', N'Database-wide wait-stat change during the capture. Not a per-request wait.'),
        (N'SignalWaitMillisecondsDelta', N'milliseconds', N'Database-wide signal-wait change. Included in the wait-time counter, not an extra total to add to it.'),
        (N'WaitingTasksDelta', N'tasks', N'Database-wide waiting-task change.'),
        (N'EventsNotRetained', N'events', N'totalEventsProcessed minus retained eventCount. Earlier events left the ring. Not a dropped-event count and not XML truncation.')
) AS units(Metric, Unit, Meaning);

SELECT
    capture.CaptureStartedAtUtc,
    ring.SnapshotStartedAtUtc,
    capture.SessionName,
    capture.ClientAppName,
    N'stopped after snapshot' AS SessionState
FROM #AvatarProfileCapture AS capture
CROSS JOIN #AvatarProfileRingBuffer AS ring;

SELECT OptionName, OptionValue
FROM #AvatarProfileOption
ORDER BY OptionName;

SELECT EventName AS ConfiguredEventName
FROM #AvatarProfileConfiguredEvent
ORDER BY EventName;

SELECT
    @DroppedCount AS RingBufferDroppedCount,
    @Truncated AS RingBufferTruncated,
    @EventCount AS RingBufferEventCount,
    @TotalProcessed AS RingBufferTotalEventsProcessed,
    @EventsNotRetained AS EventsNotRetained,
    @SessionDropped AS SessionDroppedEventCount,
    @ShreddedCount AS ShreddedEventCount,
    DATALENGTH(@TargetXml) AS RingBufferXmlBytes,
    CASE
        WHEN @DroppedCount IS NULL THEN N'The ring-buffer XML did not contain droppedCount.'
        WHEN @DroppedCount > 0 THEN N'Events were dropped. This capture is incomplete.'
        ELSE N'No dropped events reported by the ring buffer.'
    END AS DroppedEventNote,
    CASE
        WHEN @TotalProcessed IS NULL OR @EventCount IS NULL THEN N'The ring-buffer XML did not contain both totalEventsProcessed and eventCount.'
        WHEN @TotalProcessed > @EventCount THEN N'Earlier events are no longer retained. totalEventsProcessed exceeds eventCount.'
        ELSE N'All processed events are still retained. totalEventsProcessed does not exceed eventCount.'
    END AS RetentionNote,
    CASE
        WHEN @Truncated IS NULL THEN N'The ring-buffer XML did not contain truncated.'
        WHEN @Truncated <> 0 THEN N'The ring-buffer XML was truncated.'
        ELSE N'The ring-buffer XML truncated attribute is 0.'
    END AS TruncationNote,
    CASE
        WHEN @EventCount IS NULL THEN N'The ring-buffer XML did not contain eventCount.'
        WHEN @ShreddedCount <> @EventCount THEN N'Shredded row count does not match the ring-buffer eventCount.'
        ELSE N'Shredded row count matches the ring-buffer eventCount.'
    END AS ShredNote,
    CASE
        WHEN @SessionDropped IS NULL THEN N'Session dropped_event_count was not available.'
        WHEN @SessionDropped > 0 THEN N'The session reports dropped events.'
        ELSE N'The session dropped_event_count is 0.'
    END AS SessionDroppedNote;

SELECT
    EventName,
    COUNT(*) AS EventCount
FROM #AvatarProfileEvents
GROUP BY EventName
ORDER BY EventName;

SELECT
    ClientConnectionId,
    MIN(EventAtUtc) AS FirstEventAtUtc,
    MAX(EventAtUtc) AS LastEventAtUtc,
    COUNT(*) AS EventCount,
    N'Match ClientConnectionId to AvatarGet. Do not treat LastEventAtUtc minus FirstEventAtUtc as TotalMs, and do not add event durations.' AS CorrelationNote
FROM #AvatarProfileEvents
GROUP BY ClientConnectionId
ORDER BY FirstEventAtUtc, ClientConnectionId;

SELECT
    EventSequence,
    EventName,
    EventAtUtc,
    EventAtUtcText,
    ClientConnectionId,
    SessionId,
    ClientAppName,
    DurationMicroseconds,
    CpuMicroseconds,
    LogicalReads,
    PhysicalReads,
    Writes,
    ResultRows,
    RpcResult,
    ObjectName,
    ErrorNumber,
    Severity,
    ErrorState,
    LEN(SqlText) AS SqlTextLength,
    CASE WHEN LEN(SqlText) > 4000 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS SqlTextTruncatedForDisplay,
    LEFT(SqlText, 4000) AS SqlTextPrefix,
    LEN(ErrorMessage) AS ErrorMessageLength,
    CASE WHEN LEN(ErrorMessage) > 4000 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS ErrorMessageTruncatedForDisplay,
    LEFT(ErrorMessage, 4000) AS ErrorMessagePrefix
FROM #AvatarProfileEvents
ORDER BY EventAtUtc, EventSequence;

SELECT
    EventSequence,
    CAST
    (
        (
            SELECT long_text.SqlText AS [text]
            FOR XML PATH(N'SqlText'), TYPE
        ) AS xml
    ) AS SqlTextXml
FROM #AvatarProfileEvents AS long_text
WHERE LEN(long_text.SqlText) > 4000
ORDER BY EventSequence;

SELECT
    EventSequence,
    CAST
    (
        (
            SELECT long_message.ErrorMessage AS [text]
            FOR XML PATH(N'ErrorMessage'), TYPE
        ) AS xml
    ) AS ErrorMessageXml
FROM #AvatarProfileEvents AS long_message
WHERE LEN(long_message.ErrorMessage) > 4000
ORDER BY EventSequence;

SELECT
    resource_stat.end_time AS IntervalEndedAtUtc,
    CAST(15 AS int) AS IntervalLengthSeconds,
    resource_stat.avg_cpu_percent AS AvgCpuPercentOfLimit,
    resource_stat.avg_data_io_percent AS AvgDataIoPercentOfLimit,
    resource_stat.avg_log_write_percent AS AvgLogWritePercentOfLimit,
    resource_stat.max_worker_percent AS MaxWorkerPercentOfLimit,
    resource_stat.max_session_percent AS MaxSessionPercentOfLimit,
    resource_stat.dtu_limit AS DtuLimit
FROM sys.dm_db_resource_stats AS resource_stat
WHERE resource_stat.end_time > @CaptureStartedAtUtc
  AND DATEADD(SECOND, -15, resource_stat.end_time) < @SnapshotStartedAtUtc
ORDER BY resource_stat.end_time;

SELECT
    N'database-wide, not per-request' AS WaitScope,
    COALESCE(wait_end.wait_type, wait_baseline.wait_type) AS wait_type,
    ISNULL(wait_end.waiting_tasks_count, 0) - ISNULL(wait_baseline.waiting_tasks_count, 0) AS WaitingTasksDelta,
    ISNULL(wait_end.wait_time_ms, 0) - ISNULL(wait_baseline.wait_time_ms, 0) AS WaitTimeMillisecondsDelta,
    ISNULL(wait_end.signal_wait_time_ms, 0) - ISNULL(wait_baseline.signal_wait_time_ms, 0) AS SignalWaitMillisecondsDelta
FROM #AvatarProfileWaitEnd AS wait_end
FULL OUTER JOIN #AvatarProfileWaitBaseline AS wait_baseline
    ON wait_baseline.wait_type = wait_end.wait_type
WHERE ISNULL(wait_end.waiting_tasks_count, 0) <> ISNULL(wait_baseline.waiting_tasks_count, 0)
   OR ISNULL(wait_end.wait_time_ms, 0) <> ISNULL(wait_baseline.wait_time_ms, 0)
   OR ISNULL(wait_end.signal_wait_time_ms, 0) <> ISNULL(wait_baseline.signal_wait_time_ms, 0)
ORDER BY WaitTimeMillisecondsDelta DESC, wait_type;

SELECT
    N'Match HAR member URLs to AvatarGet MemberId, and AvatarGet ClientConnectionId to the event ClientConnectionId. Resource rows are completed 15-second buckets only. A negative wait delta means the database wait counters were reset, not a negative wait.' AS ReadNotes;
