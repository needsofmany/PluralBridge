-- Step 6 start
USE [PluralBridge_RepeatBuild];
GO

SELECT CheckName, ExpectedCount, ActualCount,
       CASE WHEN ExpectedCount = ActualCount THEN 'PASS' ELSE 'FAIL' END AS Status
FROM (
    SELECT 'pb_import_batches' AS CheckName, CAST(1 AS bigint) AS ExpectedCount, COUNT_BIG(*) AS ActualCount FROM dbo.pb_import_batches
    UNION ALL SELECT 'pb_systems', CAST(1 AS bigint), COUNT_BIG(*) FROM dbo.pb_systems
    UNION ALL SELECT 'pb_members', CAST(49 AS bigint), COUNT_BIG(*) FROM dbo.pb_members
    UNION ALL SELECT 'pb_privacy_buckets', CAST(2 AS bigint), COUNT_BIG(*) FROM dbo.pb_privacy_buckets
    UNION ALL SELECT 'pb_custom_fields', CAST(7 AS bigint), COUNT_BIG(*) FROM dbo.pb_custom_fields
    UNION ALL SELECT 'pb_front_history', CAST(886 AS bigint), COUNT_BIG(*) FROM dbo.pb_front_history
    UNION ALL SELECT 'pb_source_records 1-6 total', CAST(945 AS bigint), COUNT_BIG(*) FROM dbo.pb_source_records
    UNION ALL SELECT 'pb_source_id_map 1-6 total', CAST(945 AS bigint), COUNT_BIG(*) FROM dbo.pb_source_id_map
) AS v
ORDER BY CheckName;

-- Step 6 end
