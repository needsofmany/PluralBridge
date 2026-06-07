-- Step 14 start
-- Include 1 of 6: database/tracking/recovery_workflow_steps_025_create_003_header_segment.sql
-- Step 25 start
-- Removed embedded target database switch: USE [PluralBridge];
SET NOCOUNT ON;

DECLARE @SourceSystemCode nvarchar(32) = N'APPARYLLIS';
DECLARE @SourceExportName nvarchar(500) = N'PluralBridge-Prequel';
DECLARE @ImportToolName nvarchar(255) = N'Repeatable SSMS canonical population script';
DECLARE @ImportToolVersion nvarchar(64) = N'recovered-first-pass';
DECLARE @ImportNotes nvarchar(max) = N'Repeatable canonical population from preserved PluralBridge-Prequel database.';

DECLARE @ImportBatchId uniqueidentifier =
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|IMPORT_BATCH|' + @SourceExportName + N'|' + @ImportToolVersion)));

DECLARE @ImportStartedAtUtc datetime2(3);
DECLARE @ImportCompletedAtUtc datetime2(3);

WITH SourceImportTimes AS
(
    SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.me
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.members
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.privacybuckets
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.customfields
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.front_history
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.member_avatars
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.member_notes
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.chat_categories
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.chat_channels
    UNION ALL SELECT imported_at_utc FROM [PluralBridge-Prequel].dbo.friends
)
SELECT @ImportStartedAtUtc = MIN(imported_at_utc), @ImportCompletedAtUtc = MAX(imported_at_utc)
FROM SourceImportTimes;

IF NOT EXISTS (SELECT 1 FROM dbo.pb_source_systems WHERE SourceSystemCode = @SourceSystemCode)
BEGIN
    THROW 51000, 'Required source system APPARYLLIS is missing from dbo.pb_source_systems. Run 002_seed_lookup_data.sql first.', 1;
END;

INSERT INTO dbo.pb_import_batches
(
    ImportBatchId,
    SourceSystemCode,
    ImportStartedAtUtc,
    ImportCompletedAtUtc,
    ImportToolName,
    ImportToolVersion,
    SourceExportName,
    SourceExportSha256,
    Notes
)
SELECT
    @ImportBatchId,
    @SourceSystemCode,
    @ImportStartedAtUtc,
    @ImportCompletedAtUtc,
    @ImportToolName,
    @ImportToolVersion,
    @SourceExportName,
    NULL,
    @ImportNotes
WHERE NOT EXISTS (SELECT 1 FROM dbo.pb_import_batches WHERE ImportBatchId = @ImportBatchId);

SELECT import_batch_count = COUNT(*) FROM dbo.pb_import_batches WHERE ImportBatchId = @ImportBatchId;

-- Step 25 end

GO

-- Include 2 of 6: database/tracking/recovery_workflow_steps_027_create_003_system_segment.sql
-- Step 27 start
-- Removed embedded target database switch: USE [PluralBridge];
SET NOCOUNT ON;

DECLARE @SourceSystemCode nvarchar(32) = N'APPARYLLIS';
DECLARE @SourceEntityTypeCode nvarchar(64) = N'SYSTEM_PROFILE';
DECLARE @PluralBridgeEntityTypeCode nvarchar(64) = N'SYSTEM';
DECLARE @SourceEndpoint nvarchar(1000) = N'/v1/me';
DECLARE @SourceExportName nvarchar(500) = N'PluralBridge-Prequel';
DECLARE @ImportToolVersion nvarchar(64) = N'recovered-first-pass';

DECLARE @ImportBatchId uniqueidentifier =
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|IMPORT_BATCH|' + @SourceExportName + N'|' + @ImportToolVersion)));

IF OBJECT_ID(N'tempdb..#SystemSource') IS NOT NULL DROP TABLE #SystemSource;

SELECT
    SourceId = me.uid COLLATE DATABASE_DEFAULT,
    SystemId = CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|' + @SourceEntityTypeCode + N'|' + me.uid))),
    SystemName = JSON_VALUE(me.raw_json, N'$.content.name'),
    Description = JSON_VALUE(me.raw_json, N'$.content.desc'),
    Color = JSON_VALUE(me.raw_json, N'$.content.color'),
    AvatarUrl = JSON_VALUE(me.raw_json, N'$.content.avatarUrl'),
    AvatarUuid = JSON_VALUE(me.raw_json, N'$.content.avatarUuid'),
    SourceCreatedAtMs = TRY_CONVERT(bigint, JSON_VALUE(me.raw_json, N'$.content.createdAt')),
    LastOperationTimeMs = TRY_CONVERT(bigint, JSON_VALUE(me.raw_json, N'$.content.lastOperationTime')),
    RawJson = me.raw_json,
    RawJsonSha256 = HASHBYTES(N'SHA2_256', CONVERT(varbinary(max), me.raw_json)),
    ImportedAtUtc = me.imported_at_utc
INTO #SystemSource
FROM [PluralBridge-Prequel].dbo.me AS me
WHERE ISJSON(me.raw_json) = 1;

INSERT INTO dbo.pb_source_records
(
    SourceRecordId, ImportBatchId, SourceSystemCode, SourceEntityTypeCode, SourceId, SourceEndpoint, RawJson, RawJsonSha256, ImportedAtUtc
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_RECORD|' + @SourceEntityTypeCode + N'|' + ss.SourceId))),
    @ImportBatchId, @SourceSystemCode, @SourceEntityTypeCode, ss.SourceId, @SourceEndpoint, ss.RawJson, ss.RawJsonSha256, ss.ImportedAtUtc
FROM #SystemSource AS ss
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_records AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = ss.SourceId
);

INSERT INTO dbo.pb_systems
(
    SystemId, SystemName, Description, Color, AvatarUrl, AvatarUuid, SourceCreatedAtMs, LastOperationTimeMs, ImportedAtUtc
)
SELECT
    ss.SystemId, ss.SystemName, ss.Description, ss.Color, ss.AvatarUrl, ss.AvatarUuid, ss.SourceCreatedAtMs, ss.LastOperationTimeMs, ss.ImportedAtUtc
FROM #SystemSource AS ss
WHERE NOT EXISTS (SELECT 1 FROM dbo.pb_systems AS existing WHERE existing.SystemId = ss.SystemId);

INSERT INTO dbo.pb_source_id_map
(
    SourceIdMapId, SourceSystemCode, SourceEntityTypeCode, SourceId, PluralBridgeEntityTypeCode, PluralBridgeId, ImportBatchId
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_ID_MAP|' + @SourceEntityTypeCode + N'|' + ss.SourceId))),
    @SourceSystemCode, @SourceEntityTypeCode, ss.SourceId, @PluralBridgeEntityTypeCode, ss.SystemId, @ImportBatchId
FROM #SystemSource AS ss
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_id_map AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = ss.SourceId
);

SELECT
    system_count = (SELECT COUNT(*) FROM dbo.pb_systems),
    system_profile_source_record_count = (SELECT COUNT(*) FROM dbo.pb_source_records WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode),
    system_profile_source_id_map_count = (SELECT COUNT(*) FROM dbo.pb_source_id_map WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode);

-- Step 27 end

GO

-- Include 3 of 6: database/tracking/recovery_workflow_steps_031_create_003_member_segment.sql
-- Step 31 start
-- Removed embedded target database switch: USE [PluralBridge];
SET NOCOUNT ON;

DECLARE @SourceSystemCode nvarchar(32) = N'APPARYLLIS';
DECLARE @SourceEntityTypeCode nvarchar(64) = N'MEMBER';
DECLARE @PluralBridgeEntityTypeCode nvarchar(64) = N'MEMBER';
DECLARE @SourceExportName nvarchar(500) = N'PluralBridge-Prequel';
DECLARE @ImportToolVersion nvarchar(64) = N'recovered-first-pass';

DECLARE @ImportBatchId uniqueidentifier =
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|IMPORT_BATCH|' + @SourceExportName + N'|' + @ImportToolVersion)));

DECLARE @SystemSourceId nvarchar(128);
DECLARE @SystemId uniqueidentifier;
DECLARE @SourceEndpoint nvarchar(1000);

SELECT @SystemSourceId = me.uid COLLATE DATABASE_DEFAULT
FROM [PluralBridge-Prequel].dbo.me AS me;

SET @SystemId = CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SYSTEM_PROFILE|' + @SystemSourceId)));
SET @SourceEndpoint = N'/v1/members/' + @SystemSourceId;

IF NOT EXISTS (SELECT 1 FROM dbo.pb_import_batches WHERE ImportBatchId = @ImportBatchId)
BEGIN
    THROW 51001, 'Required import batch is missing. Run the 003 header/import-batch segment first.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.pb_systems WHERE SystemId = @SystemId)
BEGIN
    THROW 51002, 'Required canonical System row is missing. Run the 003 System segment first.', 1;
END;

IF OBJECT_ID(N'tempdb..#MemberSource') IS NOT NULL DROP TABLE #MemberSource;

SELECT
    SourceId = m.id COLLATE DATABASE_DEFAULT,
    MemberId = CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|' + @SourceEntityTypeCode + N'|' + m.id))),
    SystemId = @SystemId,
    DisplayName = COALESCE(m.name, JSON_VALUE(m.raw_json, N'$.content.name')),
    Pronouns = COALESCE(m.pronouns, JSON_VALUE(m.raw_json, N'$.content.pronouns')),
    Description = COALESCE(m.description, JSON_VALUE(m.raw_json, N'$.content.desc')),
    Color = JSON_VALUE(m.raw_json, N'$.content.color'),
    IsArchived = TRY_CONVERT(bit, JSON_VALUE(m.raw_json, N'$.content.archived')),
    ArchivedReason = JSON_VALUE(m.raw_json, N'$.content.archivedReason'),
    IsPrivate = TRY_CONVERT(bit, JSON_VALUE(m.raw_json, N'$.content.private')),
    PreventTrusted = TRY_CONVERT(bit, JSON_VALUE(m.raw_json, N'$.content.preventTrusted')),
    PreventsFrontNotifications = TRY_CONVERT(bit, JSON_VALUE(m.raw_json, N'$.content.preventsFrontNotifs')),
    ReceiveMessageBoardNotifications = TRY_CONVERT(bit, JSON_VALUE(m.raw_json, N'$.content.receiveMessageBoardNotifs')),
    SupportsDescriptionMarkdown = TRY_CONVERT(bit, JSON_VALUE(m.raw_json, N'$.content.supportDescMarkdown')),
    LastOperationTimeMs = TRY_CONVERT(bigint, JSON_VALUE(m.raw_json, N'$.content.lastOperationTime')),
    RawJson = m.raw_json,
    RawJsonSha256 = HASHBYTES(N'SHA2_256', CONVERT(varbinary(max), m.raw_json)),
    ImportedAtUtc = m.imported_at_utc
INTO #MemberSource
FROM [PluralBridge-Prequel].dbo.members AS m
WHERE ISJSON(m.raw_json) = 1;

INSERT INTO dbo.pb_source_records
(
    SourceRecordId, ImportBatchId, SourceSystemCode, SourceEntityTypeCode, SourceId, SourceEndpoint, RawJson, RawJsonSha256, ImportedAtUtc
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_RECORD|' + @SourceEntityTypeCode + N'|' + ms.SourceId))),
    @ImportBatchId, @SourceSystemCode, @SourceEntityTypeCode, ms.SourceId, @SourceEndpoint, ms.RawJson, ms.RawJsonSha256, ms.ImportedAtUtc
FROM #MemberSource AS ms
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_records AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = ms.SourceId
);

INSERT INTO dbo.pb_members
(
    MemberId, SystemId, DisplayName, Pronouns, Description, Color, IsArchived, ArchivedReason, IsPrivate, PreventTrusted, PreventsFrontNotifications, ReceiveMessageBoardNotifications, SupportsDescriptionMarkdown, LastOperationTimeMs, ImportedAtUtc
)
SELECT
    ms.MemberId, ms.SystemId, ms.DisplayName, ms.Pronouns, ms.Description, ms.Color, ms.IsArchived, ms.ArchivedReason, ms.IsPrivate, ms.PreventTrusted, ms.PreventsFrontNotifications, ms.ReceiveMessageBoardNotifications, ms.SupportsDescriptionMarkdown, ms.LastOperationTimeMs, ms.ImportedAtUtc
FROM #MemberSource AS ms
WHERE NOT EXISTS (SELECT 1 FROM dbo.pb_members AS existing WHERE existing.MemberId = ms.MemberId);

INSERT INTO dbo.pb_source_id_map
(
    SourceIdMapId, SourceSystemCode, SourceEntityTypeCode, SourceId, PluralBridgeEntityTypeCode, PluralBridgeId, ImportBatchId
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_ID_MAP|' + @SourceEntityTypeCode + N'|' + ms.SourceId))),
    @SourceSystemCode, @SourceEntityTypeCode, ms.SourceId, @PluralBridgeEntityTypeCode, ms.MemberId, @ImportBatchId
FROM #MemberSource AS ms
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_id_map AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = ms.SourceId
);

SELECT
    member_count = (SELECT COUNT(*) FROM dbo.pb_members),
    member_source_record_count = (SELECT COUNT(*) FROM dbo.pb_source_records WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode),
    member_source_id_map_count = (SELECT COUNT(*) FROM dbo.pb_source_id_map WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode);

-- Step 31 end

GO

-- Include 4 of 6: database/tracking/recovery_workflow_steps_033_create_003_privacy_bucket_segment.sql
-- Step 33 start
-- Removed embedded target database switch: USE [PluralBridge];
SET NOCOUNT ON;

DECLARE @SourceSystemCode nvarchar(32) = N'APPARYLLIS';
DECLARE @SourceEntityTypeCode nvarchar(64) = N'PRIVACY_BUCKET';
DECLARE @PluralBridgeEntityTypeCode nvarchar(64) = N'PRIVACY_BUCKET';
DECLARE @SourceEndpoint nvarchar(1000) = N'/v1/privacyBuckets';
DECLARE @SourceExportName nvarchar(500) = N'PluralBridge-Prequel';
DECLARE @ImportToolVersion nvarchar(64) = N'recovered-first-pass';

DECLARE @ImportBatchId uniqueidentifier =
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|IMPORT_BATCH|' + @SourceExportName + N'|' + @ImportToolVersion)));

DECLARE @SystemSourceId nvarchar(128);
DECLARE @SystemId uniqueidentifier;

SELECT @SystemSourceId = me.uid COLLATE DATABASE_DEFAULT
FROM [PluralBridge-Prequel].dbo.me AS me;

SET @SystemId = CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SYSTEM_PROFILE|' + @SystemSourceId)));

IF NOT EXISTS (SELECT 1 FROM dbo.pb_import_batches WHERE ImportBatchId = @ImportBatchId)
BEGIN
    THROW 51001, 'Required import batch is missing. Run the 003 header/import-batch segment first.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.pb_systems WHERE SystemId = @SystemId)
BEGIN
    THROW 51002, 'Required canonical System row is missing. Run the 003 System segment first.', 1;
END;

IF OBJECT_ID(N'tempdb..#PrivacyBucketSource') IS NOT NULL DROP TABLE #PrivacyBucketSource;

SELECT
    SourceId = pb.id COLLATE DATABASE_DEFAULT,
    PrivacyBucketId = CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|' + @SourceEntityTypeCode + N'|' + pb.id))),
    SystemId = @SystemId,
    BucketName = COALESCE(pb.name, JSON_VALUE(pb.raw_json, N'$.content.name')),
    Description = JSON_VALUE(pb.raw_json, N'$.content.desc'),
    Color = JSON_VALUE(pb.raw_json, N'$.content.color'),
    Icon = JSON_VALUE(pb.raw_json, N'$.content.icon'),
    RankText = JSON_VALUE(pb.raw_json, N'$.content.rank'),
    RawJson = pb.raw_json,
    RawJsonSha256 = HASHBYTES(N'SHA2_256', CONVERT(varbinary(max), pb.raw_json)),
    ImportedAtUtc = pb.imported_at_utc
INTO #PrivacyBucketSource
FROM [PluralBridge-Prequel].dbo.privacybuckets AS pb
WHERE ISJSON(pb.raw_json) = 1;

INSERT INTO dbo.pb_source_records
(
    SourceRecordId, ImportBatchId, SourceSystemCode, SourceEntityTypeCode, SourceId, SourceEndpoint, RawJson, RawJsonSha256, ImportedAtUtc
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_RECORD|' + @SourceEntityTypeCode + N'|' + pbs.SourceId))),
    @ImportBatchId, @SourceSystemCode, @SourceEntityTypeCode, pbs.SourceId, @SourceEndpoint, pbs.RawJson, pbs.RawJsonSha256, pbs.ImportedAtUtc
FROM #PrivacyBucketSource AS pbs
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_records AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = pbs.SourceId
);

INSERT INTO dbo.pb_privacy_buckets
(
    PrivacyBucketId, SystemId, BucketName, Description, Color, Icon, RankText, ImportedAtUtc
)
SELECT
    pbs.PrivacyBucketId, pbs.SystemId, pbs.BucketName, pbs.Description, pbs.Color, pbs.Icon, pbs.RankText, pbs.ImportedAtUtc
FROM #PrivacyBucketSource AS pbs
WHERE NOT EXISTS (SELECT 1 FROM dbo.pb_privacy_buckets AS existing WHERE existing.PrivacyBucketId = pbs.PrivacyBucketId);

INSERT INTO dbo.pb_source_id_map
(
    SourceIdMapId, SourceSystemCode, SourceEntityTypeCode, SourceId, PluralBridgeEntityTypeCode, PluralBridgeId, ImportBatchId
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_ID_MAP|' + @SourceEntityTypeCode + N'|' + pbs.SourceId))),
    @SourceSystemCode, @SourceEntityTypeCode, pbs.SourceId, @PluralBridgeEntityTypeCode, pbs.PrivacyBucketId, @ImportBatchId
FROM #PrivacyBucketSource AS pbs
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_id_map AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = pbs.SourceId
);

SELECT
    privacy_bucket_count = (SELECT COUNT(*) FROM dbo.pb_privacy_buckets),
    privacy_bucket_source_record_count = (SELECT COUNT(*) FROM dbo.pb_source_records WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode),
    privacy_bucket_source_id_map_count = (SELECT COUNT(*) FROM dbo.pb_source_id_map WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode);

-- Step 33 end

GO

-- Include 5 of 6: database/tracking/recovery_workflow_steps_035_create_003_custom_field_segment.sql
-- Step 35 start
-- Removed embedded target database switch: USE [PluralBridge];
SET NOCOUNT ON;

DECLARE @SourceSystemCode nvarchar(32) = N'APPARYLLIS';
DECLARE @SourceEntityTypeCode nvarchar(64) = N'CUSTOM_FIELD';
DECLARE @PluralBridgeEntityTypeCode nvarchar(64) = N'CUSTOM_FIELD';
DECLARE @SourceExportName nvarchar(500) = N'PluralBridge-Prequel';
DECLARE @ImportToolVersion nvarchar(64) = N'recovered-first-pass';

DECLARE @ImportBatchId uniqueidentifier =
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|IMPORT_BATCH|' + @SourceExportName + N'|' + @ImportToolVersion)));

DECLARE @SystemSourceId nvarchar(128);
DECLARE @SystemId uniqueidentifier;
DECLARE @SourceEndpoint nvarchar(1000);

SELECT @SystemSourceId = me.uid COLLATE DATABASE_DEFAULT
FROM [PluralBridge-Prequel].dbo.me AS me;

SET @SystemId = CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SYSTEM_PROFILE|' + @SystemSourceId)));
SET @SourceEndpoint = N'/v1/customFields/' + @SystemSourceId;

IF NOT EXISTS (SELECT 1 FROM dbo.pb_import_batches WHERE ImportBatchId = @ImportBatchId)
BEGIN
    THROW 51001, 'Required import batch is missing. Run the 003 header/import-batch segment first.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.pb_systems WHERE SystemId = @SystemId)
BEGIN
    THROW 51002, 'Required canonical System row is missing. Run the 003 System segment first.', 1;
END;

IF OBJECT_ID(N'tempdb..#CustomFieldSource') IS NOT NULL DROP TABLE #CustomFieldSource;

SELECT
    SourceId = cf.id COLLATE DATABASE_DEFAULT,
    CustomFieldId = CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|' + @SourceEntityTypeCode + N'|' + cf.id))),
    SystemId = @SystemId,
    FieldName = COALESCE(cf.name, JSON_VALUE(cf.raw_json, N'$.content.name')),
    Description = JSON_VALUE(cf.raw_json, N'$.content.desc'),
    FieldTypeCode = TRY_CONVERT(int, COALESCE(cf.type, JSON_VALUE(cf.raw_json, N'$.content.type'))),
    DisplayOrderText = JSON_VALUE(cf.raw_json, N'$.content.order'),
    SupportsMarkdown = TRY_CONVERT(bit, JSON_VALUE(cf.raw_json, N'$.content.supportMarkdown')),
    RawJson = cf.raw_json,
    RawJsonSha256 = HASHBYTES(N'SHA2_256', CONVERT(varbinary(max), cf.raw_json)),
    ImportedAtUtc = cf.imported_at_utc
INTO #CustomFieldSource
FROM [PluralBridge-Prequel].dbo.customfields AS cf
WHERE ISJSON(cf.raw_json) = 1;

INSERT INTO dbo.pb_source_records
(
    SourceRecordId, ImportBatchId, SourceSystemCode, SourceEntityTypeCode, SourceId, SourceEndpoint, RawJson, RawJsonSha256, ImportedAtUtc
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_RECORD|' + @SourceEntityTypeCode + N'|' + cfs.SourceId))),
    @ImportBatchId, @SourceSystemCode, @SourceEntityTypeCode, cfs.SourceId, @SourceEndpoint, cfs.RawJson, cfs.RawJsonSha256, cfs.ImportedAtUtc
FROM #CustomFieldSource AS cfs
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_records AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = cfs.SourceId
);

INSERT INTO dbo.pb_custom_fields
(
    CustomFieldId, SystemId, FieldName, Description, FieldTypeCode, DisplayOrderText, SupportsMarkdown, ImportedAtUtc
)
SELECT
    cfs.CustomFieldId, cfs.SystemId, cfs.FieldName, cfs.Description, cfs.FieldTypeCode, cfs.DisplayOrderText, cfs.SupportsMarkdown, cfs.ImportedAtUtc
FROM #CustomFieldSource AS cfs
WHERE NOT EXISTS (SELECT 1 FROM dbo.pb_custom_fields AS existing WHERE existing.CustomFieldId = cfs.CustomFieldId);

INSERT INTO dbo.pb_source_id_map
(
    SourceIdMapId, SourceSystemCode, SourceEntityTypeCode, SourceId, PluralBridgeEntityTypeCode, PluralBridgeId, ImportBatchId
)
SELECT
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|SOURCE_ID_MAP|' + @SourceEntityTypeCode + N'|' + cfs.SourceId))),
    @SourceSystemCode, @SourceEntityTypeCode, cfs.SourceId, @PluralBridgeEntityTypeCode, cfs.CustomFieldId, @ImportBatchId
FROM #CustomFieldSource AS cfs
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.pb_source_id_map AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = cfs.SourceId
);

SELECT
    custom_field_count = (SELECT COUNT(*) FROM dbo.pb_custom_fields),
    custom_field_source_record_count = (SELECT COUNT(*) FROM dbo.pb_source_records WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode),
    custom_field_source_id_map_count = (SELECT COUNT(*) FROM dbo.pb_source_id_map WHERE SourceSystemCode = @SourceSystemCode AND SourceEntityTypeCode = @SourceEntityTypeCode);

-- Step 35 end

GO

-- Include 6 of 6: database/tracking/recovery_workflow_steps_089q_create_003_front_history_segment_corrected.sql
-- Step 89Q start
-- Corrected 003 front-history population SQL segment
-- Tracking segment only. Do not promote to database/populate without explicit approval.

SET NOCOUNT ON;

-- Step 14 patch: segment 6 uses the deterministic import batch id created by segment 1.
DECLARE @SourceSystemCode nvarchar(32) = N'APPARYLLIS';
DECLARE @SourceExportName nvarchar(500) = N'PluralBridge-Prequel';
DECLARE @ImportToolVersion nvarchar(64) = N'recovered-first-pass';
DECLARE @ImportBatchId uniqueidentifier =
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), @SourceSystemCode + N'|IMPORT_BATCH|' + @SourceExportName + N'|' + @ImportToolVersion)));

IF NOT EXISTS (SELECT 1 FROM dbo.pb_import_batches WHERE ImportBatchId = @ImportBatchId)
BEGIN
    THROW 51001, 'Expected import batch row is missing before FRONT_HISTORY population.', 1;
END;
DECLARE @SourceEntityTypeCode nvarchar(64) = N'FRONT_HISTORY';
DECLARE @PluralBridgeEntityTypeCode nvarchar(64) = N'FRONT_HISTORY';
DECLARE @SourceEndpoint nvarchar(1000) = N'/v1/frontHistory';

IF OBJECT_ID(N'tempdb..#SourceRows') IS NOT NULL
    DROP TABLE #SourceRows;

;WITH RawRows AS
(
    SELECT
        CONVERT(nvarchar(255), fh.id) COLLATE DATABASE_DEFAULT AS SourceId,
        CONVERT(nvarchar(255), fh.member_id) COLLATE DATABASE_DEFAULT AS MemberSourceId,
        fh.start_time AS StartTimeMs,
        fh.end_time AS EndTimeMs,
        TRY_CONVERT(bit, JSON_VALUE(fh.raw_json, '$.content.live')) AS IsLive,
        TRY_CONVERT(bit, JSON_VALUE(fh.raw_json, '$.content.custom')) AS IsCustom,
        JSON_VALUE(fh.raw_json, '$.content.customStatus') AS CustomStatus,
        TRY_CONVERT(bigint, JSON_VALUE(fh.raw_json, '$.content.lastOperationTime')) AS LastOperationTimeMs,
        fh.raw_json AS RawJson,
        HASHBYTES('SHA2_256', CONVERT(varbinary(max), fh.raw_json)) AS RawJsonSha256,
        fh.imported_at_utc AS ImportedAtUtc,
        ROW_NUMBER() OVER
        (
            PARTITION BY CONVERT(nvarchar(255), fh.id) COLLATE DATABASE_DEFAULT
            ORDER BY fh.imported_at_utc DESC, fh.start_time DESC, fh.end_time DESC
        ) AS SourceRank
    FROM [PluralBridge-Prequel].dbo.front_history AS fh
)
SELECT
    rr.SourceId,
    rr.MemberSourceId,
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), CONCAT(N'APPARYLLIS|FRONT_HISTORY|', rr.SourceId)))) AS FrontHistoryId,
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), CONCAT(N'APPARYLLIS|SOURCE_RECORD|FRONT_HISTORY|', rr.SourceId)))) AS SourceRecordId,
    CONVERT(uniqueidentifier, HASHBYTES(N'MD5', CONVERT(varbinary(max), CONCAT(N'APPARYLLIS|SOURCE_ID_MAP|FRONT_HISTORY|', rr.SourceId)))) AS SourceIdMapId,
    sysmap.PluralBridgeId AS SystemId,
    membermap.PluralBridgeId AS MemberId,
    rr.StartTimeMs,
    rr.EndTimeMs,
    rr.IsLive,
    rr.IsCustom,
    rr.CustomStatus,
    rr.LastOperationTimeMs,
    rr.RawJson,
    rr.RawJsonSha256,
    rr.ImportedAtUtc
INTO #SourceRows
FROM RawRows AS rr
INNER JOIN dbo.pb_source_id_map AS sysmap
    ON sysmap.SourceSystemCode = @SourceSystemCode
   AND sysmap.SourceEntityTypeCode = N'SYSTEM_PROFILE'
   AND sysmap.SourceId = N'bVP6Jt40jqYWC2tfp54TZlg0sqq2'
INNER JOIN dbo.pb_source_id_map AS membermap
    ON membermap.SourceSystemCode = @SourceSystemCode
   AND membermap.SourceEntityTypeCode = N'MEMBER'
   AND membermap.SourceId = rr.MemberSourceId
WHERE rr.SourceRank = 1;

CREATE UNIQUE CLUSTERED INDEX IX_SourceRows_SourceId ON #SourceRows(SourceId);

INSERT INTO dbo.pb_front_history
(
    FrontHistoryId,
    SystemId,
    MemberId,
    StartTimeMs,
    EndTimeMs,
    IsLive,
    IsCustom,
    CustomStatus,
    LastOperationTimeMs,
    ImportedAtUtc,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    sr.FrontHistoryId,
    sr.SystemId,
    sr.MemberId,
    sr.StartTimeMs,
    sr.EndTimeMs,
    sr.IsLive,
    sr.IsCustom,
    sr.CustomStatus,
    sr.LastOperationTimeMs,
    sr.ImportedAtUtc,
    sr.ImportedAtUtc AS CreatedAtUtc,
    sr.ImportedAtUtc AS UpdatedAtUtc
FROM #SourceRows AS sr
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_front_history AS existing
    WHERE existing.FrontHistoryId = sr.FrontHistoryId
);

INSERT INTO dbo.pb_source_records
(
    SourceRecordId,
    ImportBatchId,
    SourceSystemCode,
    SourceEntityTypeCode,
    SourceId,
    SourceEndpoint,
    RawJson,
    RawJsonSha256,
    ImportedAtUtc
)
SELECT
    sr.SourceRecordId,
    @ImportBatchId,
    @SourceSystemCode,
    @SourceEntityTypeCode,
    sr.SourceId,
    @SourceEndpoint,
    sr.RawJson,
    sr.RawJsonSha256,
    sr.ImportedAtUtc
FROM #SourceRows AS sr
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_source_records AS existing
    WHERE existing.SourceRecordId = sr.SourceRecordId
)
AND NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_source_records AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = sr.SourceId
);

INSERT INTO dbo.pb_source_id_map
(
    SourceIdMapId,
    SourceSystemCode,
    SourceEntityTypeCode,
    SourceId,
    PluralBridgeEntityTypeCode,
    PluralBridgeId,
    ImportBatchId,
    CreatedAtUtc
)
SELECT
    sr.SourceIdMapId,
    @SourceSystemCode,
    @SourceEntityTypeCode,
    sr.SourceId,
    @PluralBridgeEntityTypeCode,
    sr.FrontHistoryId,
    @ImportBatchId,
    SYSUTCDATETIME()
FROM #SourceRows AS sr
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_source_id_map AS existing
    WHERE existing.SourceIdMapId = sr.SourceIdMapId
)
AND NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_source_id_map AS existing
    WHERE existing.SourceSystemCode = @SourceSystemCode
      AND existing.SourceEntityTypeCode = @SourceEntityTypeCode
      AND existing.SourceId = sr.SourceId
);

SELECT 'pb_front_history expected 886' AS CheckName, COUNT(*) AS ActualCount FROM dbo.pb_front_history;
SELECT 'FRONT_HISTORY source_records expected 886' AS CheckName, COUNT(*) AS ActualCount FROM dbo.pb_source_records WHERE SourceSystemCode = N'APPARYLLIS' AND SourceEntityTypeCode = N'FRONT_HISTORY';
SELECT 'FRONT_HISTORY source_id_map expected 886' AS CheckName, COUNT(*) AS ActualCount FROM dbo.pb_source_id_map WHERE SourceSystemCode = N'APPARYLLIS' AND SourceEntityTypeCode = N'FRONT_HISTORY';

IF OBJECT_ID(N'tempdb..#SourceRows') IS NOT NULL
    DROP TABLE #SourceRows;

-- Step 89Q end

GO

-- Step 1 end
-- Step 14 end
