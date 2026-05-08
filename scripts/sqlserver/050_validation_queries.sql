/*
PluralBridge SQL Server script
050_validation_queries.sql

Validation queries for row counts, missing relationships, duplicate keys, and date sanity checks.

This script contains queries only.
It contains no exported user data.
*/

USE PluralBridge;
GO

PRINT 'Row counts';
GO

SELECT 'user' AS table_name, COUNT(*) AS row_count FROM dbo.[user]
UNION ALL SELECT 'me', COUNT(*) FROM dbo.me
UNION ALL SELECT 'members', COUNT(*) FROM dbo.members
UNION ALL SELECT 'front_history', COUNT(*) FROM dbo.front_history
UNION ALL SELECT 'customfields', COUNT(*) FROM dbo.customfields
UNION ALL SELECT 'member_info_values', COUNT(*) FROM dbo.member_info_values
UNION ALL SELECT 'privacybuckets', COUNT(*) FROM dbo.privacybuckets
UNION ALL SELECT 'member_buckets', COUNT(*) FROM dbo.member_buckets
UNION ALL SELECT 'chat_categories', COUNT(*) FROM dbo.chat_categories
UNION ALL SELECT 'chat_channels', COUNT(*) FROM dbo.chat_channels
UNION ALL SELECT 'friends', COUNT(*) FROM dbo.friends
UNION ALL SELECT 'member_avatars', COUNT(*) FROM dbo.member_avatars
ORDER BY table_name;
GO

PRINT 'Missing relationship checks';
GO

SELECT 'me with missing user' AS check_name, COUNT(*) AS problem_count
FROM dbo.me AS me
LEFT JOIN dbo.[user] AS u
    ON u.uid = me.uid
WHERE u.uid IS NULL
UNION ALL
SELECT 'members with missing user', COUNT(*)
FROM dbo.members AS m
LEFT JOIN dbo.[user] AS u
    ON u.uid = m.system_uid
WHERE m.system_uid IS NOT NULL
  AND u.uid IS NULL
UNION ALL
SELECT 'front_history with missing member', COUNT(*)
FROM dbo.front_history AS fh
LEFT JOIN dbo.members AS m
    ON m.id = fh.member_id
WHERE fh.member_id IS NOT NULL
  AND m.id IS NULL
UNION ALL
SELECT 'member_info_values with missing member', COUNT(*)
FROM dbo.member_info_values AS miv
LEFT JOIN dbo.members AS m
    ON m.id = miv.member_id
WHERE m.id IS NULL
UNION ALL
SELECT 'member_info_values with missing customfield', COUNT(*)
FROM dbo.member_info_values AS miv
LEFT JOIN dbo.customfields AS cf
    ON cf.id = miv.field_id
WHERE cf.id IS NULL
UNION ALL
SELECT 'member_buckets with missing member', COUNT(*)
FROM dbo.member_buckets AS mb
LEFT JOIN dbo.members AS m
    ON m.id = mb.member_id
WHERE m.id IS NULL
UNION ALL
SELECT 'member_buckets with missing privacybucket', COUNT(*)
FROM dbo.member_buckets AS mb
LEFT JOIN dbo.privacybuckets AS pb
    ON pb.id = mb.bucket_id
WHERE pb.id IS NULL
UNION ALL
SELECT 'chat_channels with missing category', COUNT(*)
FROM dbo.chat_channels AS cc
LEFT JOIN dbo.chat_categories AS cat
    ON cat.id = cc.category_id
WHERE cc.category_id IS NOT NULL
  AND cat.id IS NULL
UNION ALL
SELECT 'member_avatars with missing member', COUNT(*)
FROM dbo.member_avatars AS ma
LEFT JOIN dbo.members AS m
    ON m.id = ma.member_id
WHERE m.id IS NULL;
GO

PRINT 'Duplicate key checks';
GO

SELECT 'user duplicate uid' AS check_name, COUNT(*) AS duplicate_group_count
FROM
(
    SELECT uid
    FROM dbo.[user]
    GROUP BY uid
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'members duplicate id', COUNT(*)
FROM
(
    SELECT id
    FROM dbo.members
    GROUP BY id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'front_history duplicate id', COUNT(*)
FROM
(
    SELECT id
    FROM dbo.front_history
    GROUP BY id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'customfields duplicate id', COUNT(*)
FROM
(
    SELECT id
    FROM dbo.customfields
    GROUP BY id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'privacybuckets duplicate id', COUNT(*)
FROM
(
    SELECT id
    FROM dbo.privacybuckets
    GROUP BY id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'member_info_values duplicate member_id + field_id', COUNT(*)
FROM
(
    SELECT member_id, field_id
    FROM dbo.member_info_values
    GROUP BY member_id, field_id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'member_buckets duplicate member_id + bucket_id', COUNT(*)
FROM
(
    SELECT member_id, bucket_id
    FROM dbo.member_buckets
    GROUP BY member_id, bucket_id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'chat_categories duplicate id', COUNT(*)
FROM
(
    SELECT id
    FROM dbo.chat_categories
    GROUP BY id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'chat_channels duplicate id', COUNT(*)
FROM
(
    SELECT id
    FROM dbo.chat_channels
    GROUP BY id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'friends duplicate id', COUNT(*)
FROM
(
    SELECT id
    FROM dbo.friends
    GROUP BY id
    HAVING COUNT(*) > 1
) AS d
UNION ALL
SELECT 'member_avatars duplicate member_id', COUNT(*)
FROM
(
    SELECT member_id
    FROM dbo.member_avatars
    GROUP BY member_id
    HAVING COUNT(*) > 1
) AS d;
GO

PRINT 'Front-history sanity checks';
GO

SELECT
    SUM(CASE WHEN start_time IS NULL THEN 1 ELSE 0 END) AS missing_start_time_count,
    SUM(CASE WHEN end_time IS NULL THEN 1 ELSE 0 END) AS current_or_missing_end_time_count,
    SUM(CASE WHEN end_time IS NOT NULL AND end_time < start_time THEN 1 ELSE 0 END) AS end_before_start_count,
    SUM(CASE WHEN end_time IS NOT NULL AND end_time = start_time THEN 1 ELSE 0 END) AS zero_length_range_count
FROM dbo.front_history;
GO

PRINT 'Current front rows';
GO

SELECT
    front_history_id,
    member_id,
    member_name,
    start_datetime_utc,
    start_datetime_pacific
FROM dbo.v_current_front
ORDER BY start_datetime_utc DESC;
GO

PRINT 'Members without avatar metadata';
GO

SELECT
    m.id AS member_id,
    m.name AS member_name
FROM dbo.members AS m
LEFT JOIN dbo.member_avatars AS ma
    ON ma.member_id = m.id
WHERE ma.member_id IS NULL
ORDER BY m.name;
GO
