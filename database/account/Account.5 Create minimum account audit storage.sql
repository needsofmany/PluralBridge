DECLARE @ScriptTitle NVARCHAR(200) = N'Account.5 Create minimum account audit storage';

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    DECLARE @WrongDatabaseMessage NVARCHAR(400) = @ScriptTitle + N': wrong database selected.';
    THROW 51000, @WrongDatabaseMessage, 1;
END;

IF OBJECT_ID(N'dbo.pb_accounts', N'U') IS NULL
BEGIN
    DECLARE @MissingAccountsMessage NVARCHAR(400) = @ScriptTitle + N': dbo.pb_accounts does not exist.';
    THROW 51001, @MissingAccountsMessage, 1;
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'AccountId') IS NULL
BEGIN
    DECLARE @MissingAccountIdMessage NVARCHAR(400) = @ScriptTitle + N': dbo.pb_accounts.AccountId does not exist.';
    THROW 51002, @MissingAccountIdMessage, 1;
END;

IF OBJECT_ID(N'dbo.pb_account_audit_events', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_account_audit_events
    (
        AccountAuditEventId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_pb_account_audit_events PRIMARY KEY
            DEFAULT NEWSEQUENTIALID(),

        CreatedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_pb_account_audit_events_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

        EventName NVARCHAR(100) NOT NULL,

        Outcome NVARCHAR(30) NOT NULL,

        ReasonCode NVARCHAR(80) NOT NULL,

        ActorAccountId UNIQUEIDENTIFIER NULL,

        TargetAccountId UNIQUEIDENTIFIER NULL,

        SystemId UNIQUEIDENTIFIER NULL,

        MembershipId UNIQUEIDENTIFIER NULL,

        CorrelationId NVARCHAR(100) NOT NULL,

        Source NVARCHAR(30) NOT NULL,

        SafeSubject NVARCHAR(100) NULL,

        SafeDetailJson NVARCHAR(MAX) NULL,

        SchemaVersion INT NOT NULL
            CONSTRAINT DF_pb_account_audit_events_SchemaVersion DEFAULT (1),

        CONSTRAINT FK_pb_account_audit_events_ActorAccount
            FOREIGN KEY (ActorAccountId)
            REFERENCES dbo.pb_accounts(AccountId),

        CONSTRAINT FK_pb_account_audit_events_TargetAccount
            FOREIGN KEY (TargetAccountId)
            REFERENCES dbo.pb_accounts(AccountId),

        CONSTRAINT CK_pb_account_audit_events_EventName
            CHECK (EventName IN
            (
                N'account.registration.requested',
                N'account.registration.rejected',
                N'account.registration.created',
                N'account.registration.verified',
                N'account.registration.verification_rejected',
                N'account.code.issued',
                N'account.code.accepted',
                N'account.code.rejected',
                N'account.code.consumed',
                N'account.login.succeeded',
                N'account.login.rejected',
                N'account.logout.succeeded',
                N'account.session.rejected',
                N'account.username_recovery.requested',
                N'account.username_recovery.issued',
                N'account.username_recovery.rejected',
                N'account.username_recovery.completed',
                N'account.password_reset.requested',
                N'account.password_reset.issued',
                N'account.password_reset.code_accepted',
                N'account.password_reset.rejected',
                N'account.password_reset.completed',
                N'account.password_change.requested',
                N'account.password_change.rejected',
                N'account.password_change.completed',
                N'account.profile.updated',
                N'account.profile.rejected',
                N'account.contact.updated',
                N'account.contact.rejected',
                N'account.ownership.bootstrap_started',
                N'account.ownership.system_created',
                N'account.ownership.membership_created',
                N'account.ownership.role_assigned',
                N'account.ownership.bootstrap_completed',
                N'account.ownership.bootstrap_rejected',
                N'system.owned_profile.updated',
                N'system.owned_profile.rejected',
                N'system.owned_status.changed',
                N'system.owned_status.rejected'
            )),

        CONSTRAINT CK_pb_account_audit_events_Outcome
            CHECK (Outcome IN
            (
                N'succeeded',
                N'rejected',
                N'denied',
                N'expired',
                N'consumed',
                N'blocked',
                N'failed',
                N'no_op'
            )),

        CONSTRAINT CK_pb_account_audit_events_ReasonCode
            CHECK (ReasonCode IN
            (
                N'none',
                N'invalid_request',
                N'invalid_credentials',
                N'invalid_code',
                N'expired_code',
                N'consumed_code',
                N'purpose_mismatch',
                N'password_policy_failed',
                N'duplicate_account_identifier',
                N'account_unavailable',
                N'contact_unavailable',
                N'ownership_required',
                N'membership_required',
                N'role_required',
                N'system_unavailable',
                N'rate_limited',
                N'csrf_rejected',
                N'session_required',
                N'validation_failed',
                N'storage_failed',
                N'delivery_failed',
                N'unexpected_failure'
            )),

        CONSTRAINT CK_pb_account_audit_events_Source
            CHECK (Source IN
            (
                N'browser',
                N'api',
                N'system',
                N'import'
            )),

        CONSTRAINT CK_pb_account_audit_events_SafeDetailJson
            CHECK (SafeDetailJson IS NULL OR ISJSON(SafeDetailJson) = 1),

        CONSTRAINT CK_pb_account_audit_events_SchemaVersion
            CHECK (SchemaVersion = 1)
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_audit_events_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.pb_account_audit_events')
)
BEGIN
    CREATE INDEX IX_pb_account_audit_events_CreatedAtUtc
        ON dbo.pb_account_audit_events(CreatedAtUtc);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_audit_events_EventName_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.pb_account_audit_events')
)
BEGIN
    CREATE INDEX IX_pb_account_audit_events_EventName_CreatedAtUtc
        ON dbo.pb_account_audit_events(EventName, CreatedAtUtc);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_audit_events_ActorAccountId_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.pb_account_audit_events')
)
BEGIN
    CREATE INDEX IX_pb_account_audit_events_ActorAccountId_CreatedAtUtc
        ON dbo.pb_account_audit_events(ActorAccountId, CreatedAtUtc);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_audit_events_TargetAccountId_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.pb_account_audit_events')
)
BEGIN
    CREATE INDEX IX_pb_account_audit_events_TargetAccountId_CreatedAtUtc
        ON dbo.pb_account_audit_events(TargetAccountId, CreatedAtUtc);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_audit_events_CorrelationId'
      AND object_id = OBJECT_ID(N'dbo.pb_account_audit_events')
)
BEGIN
    CREATE INDEX IX_pb_account_audit_events_CorrelationId
        ON dbo.pb_account_audit_events(CorrelationId);
END;

SELECT
    @ScriptTitle AS ScriptTitle,
    N'create script completed' AS item,
    1 AS expected_flag;