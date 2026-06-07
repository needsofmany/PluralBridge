-- Cloud Step 20B start
-- Azure SQL 1-6 schema candidate, fixed SQL string quoting.
-- Run in SSMS against PluralBridgeCloudProof001 on pluralbridge-cloudproof-syf001.database.windows.net.
-- Requires the target database to be empty of the pb_* tables listed below.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS
(
    SELECT 1
    FROM sys.tables AS t
    WHERE t.name IN
    (
        N'pb_source_systems',
        N'pb_import_batches',
        N'pb_systems',
        N'pb_members',
        N'pb_privacy_buckets',
        N'pb_custom_fields',
        N'pb_front_history',
        N'pb_source_records',
        N'pb_source_id_map'
    )
)
BEGIN
    THROW 51000, 'Target database already has one or more PluralBridge 1-6 pb_* tables. Stop before schema creation.', 1;
END

BEGIN TRANSACTION;

-- Step 21 start
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.pb_source_systems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_source_systems
    (
        SourceSystemCode nvarchar(64) NOT NULL,
        DisplayName nvarchar(510) NOT NULL,
        Description nvarchar(max) NULL,
        ApiBaseUrl nvarchar(2000) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_source_systems_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_pb_source_systems PRIMARY KEY CLUSTERED (SourceSystemCode)
    );
END;

IF OBJECT_ID(N'dbo.pb_import_batches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_import_batches
    (
        ImportBatchId uniqueidentifier NOT NULL CONSTRAINT DF_pb_import_batches_ImportBatchId DEFAULT (newsequentialid()),
        SourceSystemCode nvarchar(64) NOT NULL,
        ImportStartedAtUtc datetime2(3) NOT NULL,
        ImportCompletedAtUtc datetime2(3) NULL,
        ImportToolName nvarchar(510) NULL,
        ImportToolVersion nvarchar(128) NULL,
        SourceExportName nvarchar(1000) NULL,
        SourceExportSha256 varbinary(32) NULL,
        Notes nvarchar(max) NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_import_batches_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_pb_import_batches PRIMARY KEY CLUSTERED (ImportBatchId)
    );
END;

IF OBJECT_ID(N'dbo.pb_systems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_systems
    (
        SystemId uniqueidentifier NOT NULL CONSTRAINT DF_pb_systems_SystemId DEFAULT (newsequentialid()),
        SystemName nvarchar(510) NULL,
        Description nvarchar(max) NULL,
        Color nvarchar(64) NULL,
        AvatarUrl nvarchar(2000) NULL,
        AvatarUuid nvarchar(128) NULL,
        SourceCreatedAtMs bigint NULL,
        LastOperationTimeMs bigint NULL,
        ImportedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_systems_ImportedAtUtc DEFAULT (sysutcdatetime()),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_systems_CreatedAtUtc DEFAULT (sysutcdatetime()),
        UpdatedAtUtc datetime2(3) NULL,
        CONSTRAINT PK_pb_systems PRIMARY KEY CLUSTERED (SystemId)
    );
END;

IF OBJECT_ID(N'dbo.pb_members', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_members
    (
        MemberId uniqueidentifier NOT NULL CONSTRAINT DF_pb_members_MemberId DEFAULT (newsequentialid()),
        SystemId uniqueidentifier NOT NULL,
        DisplayName nvarchar(510) NOT NULL,
        Pronouns nvarchar(510) NULL,
        Description nvarchar(max) NULL,
        Color nvarchar(64) NULL,
        IsArchived bit NULL,
        ArchivedReason nvarchar(max) NULL,
        IsPrivate bit NULL,
        PreventTrusted bit NULL,
        PreventsFrontNotifications bit NULL,
        ReceiveMessageBoardNotifications bit NULL,
        SupportsDescriptionMarkdown bit NULL,
        LastOperationTimeMs bigint NULL,
        ImportedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_members_ImportedAtUtc DEFAULT (sysutcdatetime()),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_members_CreatedAtUtc DEFAULT (sysutcdatetime()),
        UpdatedAtUtc datetime2(3) NULL,
        CONSTRAINT PK_pb_members PRIMARY KEY CLUSTERED (MemberId)
    );
END;

IF OBJECT_ID(N'dbo.pb_privacy_buckets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_privacy_buckets
    (
        PrivacyBucketId uniqueidentifier NOT NULL CONSTRAINT DF_pb_privacy_buckets_PrivacyBucketId DEFAULT (newsequentialid()),
        SystemId uniqueidentifier NOT NULL,
        BucketName nvarchar(510) NOT NULL,
        Description nvarchar(max) NULL,
        Color nvarchar(64) NULL,
        Icon nvarchar(510) NULL,
        RankText nvarchar(128) NULL,
        ImportedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_privacy_buckets_ImportedAtUtc DEFAULT (sysutcdatetime()),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_privacy_buckets_CreatedAtUtc DEFAULT (sysutcdatetime()),
        UpdatedAtUtc datetime2(3) NULL,
        CONSTRAINT PK_pb_privacy_buckets PRIMARY KEY CLUSTERED (PrivacyBucketId)
    );
END;

IF OBJECT_ID(N'dbo.pb_custom_fields', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_custom_fields
    (
        CustomFieldId uniqueidentifier NOT NULL CONSTRAINT DF_pb_custom_fields_CustomFieldId DEFAULT (newsequentialid()),
        SystemId uniqueidentifier NOT NULL,
        FieldName nvarchar(510) NOT NULL,
        Description nvarchar(max) NULL,
        FieldTypeCode int NULL,
        DisplayOrderText nvarchar(128) NULL,
        SupportsMarkdown bit NULL,
        ImportedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_custom_fields_ImportedAtUtc DEFAULT (sysutcdatetime()),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_custom_fields_CreatedAtUtc DEFAULT (sysutcdatetime()),
        UpdatedAtUtc datetime2(3) NULL,
        CONSTRAINT PK_pb_custom_fields PRIMARY KEY CLUSTERED (CustomFieldId)
    );
END;

IF OBJECT_ID(N'dbo.pb_front_history', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_front_history
    (
        FrontHistoryId uniqueidentifier NOT NULL CONSTRAINT DF_pb_front_history_FrontHistoryId DEFAULT (newsequentialid()),
        SystemId uniqueidentifier NOT NULL,
        MemberId uniqueidentifier NOT NULL,
        StartTimeMs bigint NOT NULL,
        EndTimeMs bigint NULL,
        IsLive bit NULL,
        IsCustom bit NULL,
        CustomStatus nvarchar(510) NULL,
        LastOperationTimeMs bigint NULL,
        ImportedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_front_history_ImportedAtUtc DEFAULT (sysutcdatetime()),
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_front_history_CreatedAtUtc DEFAULT (sysutcdatetime()),
        UpdatedAtUtc datetime2(3) NULL,
        CONSTRAINT PK_pb_front_history PRIMARY KEY CLUSTERED (FrontHistoryId)
    );
END;

IF OBJECT_ID(N'dbo.pb_source_records', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_source_records
    (
        SourceRecordId uniqueidentifier NOT NULL CONSTRAINT DF_pb_source_records_SourceRecordId DEFAULT (newsequentialid()),
        ImportBatchId uniqueidentifier NOT NULL,
        SourceSystemCode nvarchar(64) NOT NULL,
        SourceEntityTypeCode nvarchar(128) NOT NULL,
        SourceId nvarchar(256) NULL,
        SourceEndpoint nvarchar(2000) NULL,
        RawJson nvarchar(max) NULL,
        RawJsonSha256 varbinary(32) NULL,
        ImportedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_source_records_ImportedAtUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_pb_source_records PRIMARY KEY CLUSTERED (SourceRecordId)
    );
END;

IF OBJECT_ID(N'dbo.pb_source_id_map', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_source_id_map
    (
        SourceIdMapId uniqueidentifier NOT NULL CONSTRAINT DF_pb_source_id_map_SourceIdMapId DEFAULT (newsequentialid()),
        SourceSystemCode nvarchar(64) NOT NULL,
        SourceEntityTypeCode nvarchar(128) NOT NULL,
        SourceId nvarchar(256) NOT NULL,
        PluralBridgeEntityTypeCode nvarchar(128) NOT NULL,
        PluralBridgeId uniqueidentifier NOT NULL,
        ImportBatchId uniqueidentifier NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_pb_source_id_map_CreatedAtUtc DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_pb_source_id_map PRIMARY KEY CLUSTERED (SourceIdMapId),
        CONSTRAINT UQ_pb_source_id_map_SourceIdentity UNIQUE NONCLUSTERED (SourceSystemCode, SourceEntityTypeCode, SourceId)
    );
END;

IF OBJECT_ID(N'dbo.FK_pb_import_batches_pb_source_systems', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_import_batches ADD CONSTRAINT FK_pb_import_batches_pb_source_systems FOREIGN KEY (SourceSystemCode) REFERENCES dbo.pb_source_systems(SourceSystemCode);
END;

IF OBJECT_ID(N'dbo.FK_pb_members_pb_systems', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_members ADD CONSTRAINT FK_pb_members_pb_systems FOREIGN KEY (SystemId) REFERENCES dbo.pb_systems(SystemId);
END;

IF OBJECT_ID(N'dbo.FK_pb_privacy_buckets_pb_systems', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_privacy_buckets ADD CONSTRAINT FK_pb_privacy_buckets_pb_systems FOREIGN KEY (SystemId) REFERENCES dbo.pb_systems(SystemId);
END;

IF OBJECT_ID(N'dbo.FK_pb_custom_fields_pb_systems', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_custom_fields ADD CONSTRAINT FK_pb_custom_fields_pb_systems FOREIGN KEY (SystemId) REFERENCES dbo.pb_systems(SystemId);
END;

IF OBJECT_ID(N'dbo.FK_pb_front_history_pb_systems', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_front_history ADD CONSTRAINT FK_pb_front_history_pb_systems FOREIGN KEY (SystemId) REFERENCES dbo.pb_systems(SystemId);
END;

IF OBJECT_ID(N'dbo.FK_pb_front_history_pb_members', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_front_history ADD CONSTRAINT FK_pb_front_history_pb_members FOREIGN KEY (MemberId) REFERENCES dbo.pb_members(MemberId);
END;

IF OBJECT_ID(N'dbo.FK_pb_source_records_pb_import_batches', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_source_records ADD CONSTRAINT FK_pb_source_records_pb_import_batches FOREIGN KEY (ImportBatchId) REFERENCES dbo.pb_import_batches(ImportBatchId);
END;

IF OBJECT_ID(N'dbo.FK_pb_source_records_pb_source_systems', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_source_records ADD CONSTRAINT FK_pb_source_records_pb_source_systems FOREIGN KEY (SourceSystemCode) REFERENCES dbo.pb_source_systems(SourceSystemCode);
END;

IF OBJECT_ID(N'dbo.FK_pb_source_id_map_pb_import_batches', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_source_id_map ADD CONSTRAINT FK_pb_source_id_map_pb_import_batches FOREIGN KEY (ImportBatchId) REFERENCES dbo.pb_import_batches(ImportBatchId);
END;

IF OBJECT_ID(N'dbo.FK_pb_source_id_map_pb_source_systems', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.pb_source_id_map ADD CONSTRAINT FK_pb_source_id_map_pb_source_systems FOREIGN KEY (SourceSystemCode) REFERENCES dbo.pb_source_systems(SourceSystemCode);
END;

SELECT PbTableCount = COUNT(*) FROM sys.tables WHERE name LIKE NCHAR(112)+NCHAR(98)+NCHAR(95)+NCHAR(37);
-- Step 21 end

COMMIT TRANSACTION;

SELECT
    t.name AS CreatedPluralBridgeTable
FROM sys.tables AS t
WHERE t.name IN
(
    N'pb_source_systems',
    N'pb_import_batches',
    N'pb_systems',
    N'pb_members',
    N'pb_privacy_buckets',
    N'pb_custom_fields',
    N'pb_front_history',
    N'pb_source_records',
    N'pb_source_id_map'
)
ORDER BY t.name;

-- Cloud Step 20B end
