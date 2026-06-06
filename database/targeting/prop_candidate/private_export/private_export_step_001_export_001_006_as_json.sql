-- Private Export Step 1 start
-- Run in SSMS with PluralBridge_RepeatBuild selected/highlighted.
-- Output: copy each JSON result cell into separate .json files later.

SET NOCOUNT ON;

SELECT ExportName = N'pb_source_systems', JsonData = (SELECT * FROM dbo.pb_source_systems ORDER BY SourceSystemCode FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_import_batches', JsonData = (SELECT * FROM dbo.pb_import_batches ORDER BY ImportBatchId FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_systems', JsonData = (SELECT * FROM dbo.pb_systems ORDER BY SystemId FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_members', JsonData = (SELECT * FROM dbo.pb_members ORDER BY MemberId FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_privacy_buckets', JsonData = (SELECT * FROM dbo.pb_privacy_buckets ORDER BY PrivacyBucketId FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_custom_fields', JsonData = (SELECT * FROM dbo.pb_custom_fields ORDER BY CustomFieldId FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_front_history', JsonData = (SELECT * FROM dbo.pb_front_history ORDER BY StartTimeMs, FrontHistoryId FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_source_records', JsonData = (SELECT * FROM dbo.pb_source_records ORDER BY SourceSystemCode, SourceEntityTypeCode, SourceId, SourceRecordId FOR JSON PATH, INCLUDE_NULL_VALUES);
SELECT ExportName = N'pb_source_id_map', JsonData = (SELECT * FROM dbo.pb_source_id_map ORDER BY SourceSystemCode, SourceEntityTypeCode, SourceId, SourceIdMapId FOR JSON PATH, INCLUDE_NULL_VALUES);

SELECT CheckName = N'pb_import_batches', ActualCount = COUNT(*) FROM dbo.pb_import_batches;
SELECT CheckName = N'pb_systems', ActualCount = COUNT(*) FROM dbo.pb_systems;
SELECT CheckName = N'pb_members', ActualCount = COUNT(*) FROM dbo.pb_members;
SELECT CheckName = N'pb_privacy_buckets', ActualCount = COUNT(*) FROM dbo.pb_privacy_buckets;
SELECT CheckName = N'pb_custom_fields', ActualCount = COUNT(*) FROM dbo.pb_custom_fields;
SELECT CheckName = N'pb_front_history', ActualCount = COUNT(*) FROM dbo.pb_front_history;
SELECT CheckName = N'pb_source_records', ActualCount = COUNT(*) FROM dbo.pb_source_records;
SELECT CheckName = N'pb_source_id_map', ActualCount = COUNT(*) FROM dbo.pb_source_id_map;

-- Private Export Step 1 end
