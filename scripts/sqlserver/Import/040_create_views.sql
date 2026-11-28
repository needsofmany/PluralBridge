/*
PluralBridge SQL Server script
040_create_views.sql

Creates readable SQL views over locally imported Simply Plural export data.

This script contains view definitions only.
It contains no exported user data.
*/

USE PluralBridge;
GO

CREATE OR ALTER VIEW dbo.v_front_history_readable
AS
SELECT
    fh.id AS front_history_id,
    fh.member_id,
    m.name AS member_name,
    fh.start_time AS start_time_ms,
    fh.end_time AS end_time_ms,
    DATEADD(MILLISECOND, fh.start_time % 1000,
        DATEADD(SECOND, fh.start_time / 1000, CONVERT(datetime2(3), '19700101'))) AS start_datetime_utc,
    CASE
        WHEN fh.end_time IS NULL THEN NULL
        ELSE DATEADD(MILLISECOND, fh.end_time % 1000,
            DATEADD(SECOND, fh.end_time / 1000, CONVERT(datetime2(3), '19700101')))
    END AS end_datetime_utc,
    CASE
        WHEN fh.end_time IS NULL THEN NULL
        ELSE DATEDIFF(SECOND,
            DATEADD(MILLISECOND, fh.start_time % 1000,
                DATEADD(SECOND, fh.start_time / 1000, CONVERT(datetime2(3), '19700101'))),
            DATEADD(MILLISECOND, fh.end_time % 1000,
                DATEADD(SECOND, fh.end_time / 1000, CONVERT(datetime2(3), '19700101')))
        )
    END AS duration_seconds
FROM dbo.front_history AS fh
LEFT JOIN dbo.members AS m
    ON m.id = fh.member_id;
GO

CREATE OR ALTER VIEW dbo.v_front_history_pacific
AS
SELECT
    r.front_history_id,
    r.member_id,
    r.member_name,
    r.start_time_ms,
    r.end_time_ms,
    r.start_datetime_utc,
    r.end_datetime_utc,
    r.start_datetime_utc AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time' AS start_datetime_pacific,
    r.end_datetime_utc AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time' AS end_datetime_pacific,
    r.duration_seconds
FROM dbo.v_front_history_readable AS r;
GO

CREATE OR ALTER VIEW dbo.v_current_front
AS
SELECT
    fh.front_history_id,
    fh.member_id,
    fh.member_name,
    fh.start_time_ms,
    fh.start_datetime_utc,
    fh.start_datetime_utc AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time' AS start_datetime_pacific,
    fh.duration_seconds
FROM dbo.v_front_history_readable AS fh
WHERE fh.end_time_ms IS NULL;
GO

CREATE OR ALTER VIEW dbo.v_member_info_readable
AS
SELECT
    m.id AS member_id,
    m.name AS member_name,
    cf.id AS field_id,
    cf.name AS field_name,
    cf.type AS field_type,
    miv.value AS field_value
FROM dbo.member_info_values AS miv
LEFT JOIN dbo.members AS m
    ON m.id = miv.member_id
LEFT JOIN dbo.customfields AS cf
    ON cf.id = miv.field_id;
GO

CREATE OR ALTER VIEW dbo.v_member_buckets_readable
AS
SELECT
    m.id AS member_id,
    m.name AS member_name,
    pb.id AS bucket_id,
    pb.name AS bucket_name
FROM dbo.member_buckets AS mb
LEFT JOIN dbo.members AS m
    ON m.id = mb.member_id
LEFT JOIN dbo.privacybuckets AS pb
    ON pb.id = mb.bucket_id;
GO

CREATE OR ALTER VIEW dbo.v_member_profile_summary
AS
SELECT
    m.id AS member_id,
    m.system_uid,
    m.name,
    m.pronouns,
    m.description,
    m.avatar_uuid,
    m.avatar_url,
    COUNT(DISTINCT fh.id) AS front_history_count,
    MIN(fh.start_time) AS first_front_start_time_ms,
    MAX(fh.start_time) AS most_recent_front_start_time_ms,
    COUNT(DISTINCT miv.field_id) AS custom_field_value_count,
    COUNT(DISTINCT mb.bucket_id) AS privacy_bucket_count
FROM dbo.members AS m
LEFT JOIN dbo.front_history AS fh
    ON fh.member_id = m.id
LEFT JOIN dbo.member_info_values AS miv
    ON miv.member_id = m.id
LEFT JOIN dbo.member_buckets AS mb
    ON mb.member_id = m.id
GROUP BY
    m.id,
    m.system_uid,
    m.name,
    m.pronouns,
    m.description,
    m.avatar_uuid,
    m.avatar_url;
GO

CREATE OR ALTER VIEW dbo.v_member_profile_summary_with_avatar
AS
SELECT
    mps.*,
    ma.local_filename AS avatar_filename,
    ma.local_path AS avatar_path,
    ma.source_url AS avatar_source_url,
    ma.downloaded_at AS avatar_downloaded_at
FROM dbo.v_member_profile_summary AS mps
LEFT JOIN dbo.member_avatars AS ma
    ON ma.member_id = mps.member_id;
GO
