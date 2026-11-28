-- Avatar GET extended-events capture, cleanup.
-- Run this after 02_read_capture.sql. It may run in a different SSMS window.
-- It stops pb_avatar_profile_capture if that session is running, then drops
-- that session. It does not reference any other event session.
-- Temporary tables from the start window remain until that window closes.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DatabaseName sysname = N'PluralBridgeChap2SafeSpine';
DECLARE @SessionName sysname = N'pb_avatar_profile_capture';
DECLARE @WasRunning bit = 0;
DECLARE @Existed bit = 0;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> @DatabaseName
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

IF @SessionName <> N'pb_avatar_profile_capture'
    THROW 50000, 'Stop: this cleanup is bound to pb_avatar_profile_capture.', 1;

IF EXISTS
(
    SELECT 1
    FROM sys.dm_xe_database_sessions
    WHERE name = @SessionName
)
BEGIN
    SET @WasRunning = 1;
    ALTER EVENT SESSION [pb_avatar_profile_capture] ON DATABASE STATE = STOP;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.database_event_sessions
    WHERE name = @SessionName
)
BEGIN
    SET @Existed = 1;
    DROP EVENT SESSION [pb_avatar_profile_capture] ON DATABASE;
END;

SELECT
    @SessionName AS SessionName,
    CASE WHEN @WasRunning = 1 THEN N'stopped' ELSE N'was not running' END AS StopResult,
    CASE WHEN @Existed = 1 THEN N'dropped' ELSE N'was not present' END AS DropResult,
    N'No other event session was referenced.' AS OtherSessions;
