/*
PluralBridge SQL Server script
060_report_queries.sql

Example report queries for locally imported Simply Plural data.

This script contains queries only.
It contains no exported user data.
*/

USE PluralBridge;
GO

PRINT 'Current front';
GO

SELECT
    member_name,
    start_datetime_utc,
    start_datetime_pacific,
    DATEDIFF(MINUTE, start_datetime_utc, SYSUTCDATETIME()) AS duration_minutes_so_far
FROM dbo.v_current_front
ORDER BY start_datetime_utc DESC;
GO

PRINT 'Fronting history summary by member';
GO

SELECT
    member_name,
    COUNT(*) AS front_history_count,
    MIN(start_datetime_utc) AS first_front_utc,
    MAX(start_datetime_utc) AS most_recent_front_utc,
    SUM(CASE WHEN duration_seconds IS NULL THEN 0 ELSE duration_seconds END) AS completed_front_seconds,
    CAST(SUM(CASE WHEN duration_seconds IS NULL THEN 0 ELSE duration_seconds END) / 3600.0 AS decimal(18, 2)) AS completed_front_hours
FROM dbo.v_front_history_readable
GROUP BY member_name
ORDER BY front_history_count DESC, member_name;
GO

PRINT 'Recent fronting history';
GO

SELECT TOP (100)
    member_name,
    start_datetime_utc,
    end_datetime_utc,
    duration_seconds,
    CAST(duration_seconds / 3600.0 AS decimal(18, 2)) AS duration_hours
FROM dbo.v_front_history_readable
ORDER BY start_datetime_utc DESC;
GO

PRINT 'Member profile summary';
GO

SELECT
    name,
    pronouns,
    front_history_count,
    custom_field_value_count,
    privacy_bucket_count,
    avatar_uuid,
    avatar_filename
FROM dbo.v_member_profile_summary_with_avatar
ORDER BY name;
GO

PRINT 'Custom field values by member';
GO

SELECT
    member_name,
    field_name,
    field_type,
    field_value
FROM dbo.v_member_info_readable
ORDER BY member_name, field_name;
GO

PRINT 'Privacy bucket memberships';
GO

SELECT
    bucket_name,
    member_name,
    member_id
FROM dbo.v_member_buckets_readable
ORDER BY bucket_name, member_name;
GO

PRINT 'Avatar export coverage';
GO

SELECT
    m.name AS member_name,
    m.id AS member_id,
    CASE WHEN ma.member_id IS NULL THEN 'No avatar metadata' ELSE 'Avatar metadata present' END AS avatar_status,
    ma.local_filename,
    ma.local_path,
    ma.source_url,
    ma.downloaded_at
FROM dbo.members AS m
LEFT JOIN dbo.member_avatars AS ma
    ON ma.member_id = m.id
ORDER BY avatar_status, m.name;
GO

PRINT 'Custom field coverage';
GO

SELECT
    cf.name AS field_name,
    cf.type AS field_type,
    COUNT(miv.member_id) AS member_value_count
FROM dbo.customfields AS cf
LEFT JOIN dbo.member_info_values AS miv
    ON miv.field_id = cf.id
GROUP BY
    cf.name,
    cf.type
ORDER BY
    cf.name;
GO

PRINT 'Daily fronting totals';
GO

SELECT
    CONVERT(date, start_datetime_utc) AS front_date_utc,
    member_name,
    COUNT(*) AS front_count,
    CAST(SUM(CASE WHEN duration_seconds IS NULL THEN 0 ELSE duration_seconds END) / 3600.0 AS decimal(18, 2)) AS completed_front_hours
FROM dbo.v_front_history_readable
GROUP BY
    CONVERT(date, start_datetime_utc),
    member_name
ORDER BY
    front_date_utc DESC,
    member_name;
GO


PRINT 'Member notes coverage';
GO

SELECT
    m.name AS member_name,
    m.id AS member_id,
    COUNT(mn.note_file) AS note_file_count,
    MIN(mn.note_file) AS first_note_file,
    MAX(mn.note_file) AS last_note_file
FROM dbo.members AS m
LEFT JOIN dbo.member_notes AS mn
    ON mn.member_id = m.id
GROUP BY
    m.name,
    m.id
ORDER BY
    m.name;
GO

PRINT 'Member note files';
GO

SELECT
    m.name AS member_name,
    mn.member_id,
    mn.note_file,
    mn.note_index,
    mn.endpoint,
    mn.ok,
    CASE
        WHEN mn.raw_json IS NULL THEN 'missing'
        WHEN ISJSON(mn.raw_json) = 1 THEN 'valid json'
        ELSE 'invalid json'
    END AS raw_json_status,
    DATALENGTH(mn.raw_json) AS raw_json_bytes
FROM dbo.member_notes AS mn
LEFT JOIN dbo.members AS m
    ON m.id = mn.member_id
ORDER BY
    m.name,
    mn.note_index,
    mn.note_file;
GO
