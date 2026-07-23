DECLARE @ScriptTitle NVARCHAR(200) = N'Account.5 Verify minimum account audit storage';

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    DECLARE @WrongDatabaseMessage NVARCHAR(400) = @ScriptTitle + N': wrong database selected.';
    THROW 51000, @WrongDatabaseMessage, 1;
END;

DECLARE @AuditObjectId INT = OBJECT_ID(N'dbo.pb_account_audit_events', N'U');

WITH checks AS
(
    SELECT 1 AS sort_order, N'current database is PluralBridgeChap2SafeSpine' AS item,
        CASE WHEN DB_NAME() = N'PluralBridgeChap2SafeSpine' THEN 1 ELSE 0 END AS expected_flag

    UNION ALL SELECT 2, N'dbo.pb_account_audit_events exists',
        CASE WHEN @AuditObjectId IS NOT NULL THEN 1 ELSE 0 END

    UNION ALL SELECT 3, N'dbo.pb_account_audit_events.AccountAuditEventId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'AccountAuditEventId') THEN 1 ELSE 0 END

    UNION ALL SELECT 4, N'dbo.pb_account_audit_events.CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'CreatedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 5, N'dbo.pb_account_audit_events.EventName exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'EventName') THEN 1 ELSE 0 END

    UNION ALL SELECT 6, N'dbo.pb_account_audit_events.Outcome exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'Outcome') THEN 1 ELSE 0 END

    UNION ALL SELECT 7, N'dbo.pb_account_audit_events.ReasonCode exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'ReasonCode') THEN 1 ELSE 0 END

    UNION ALL SELECT 8, N'dbo.pb_account_audit_events.ActorAccountId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'ActorAccountId') THEN 1 ELSE 0 END

    UNION ALL SELECT 9, N'dbo.pb_account_audit_events.TargetAccountId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'TargetAccountId') THEN 1 ELSE 0 END

    UNION ALL SELECT 10, N'dbo.pb_account_audit_events.SystemId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'SystemId') THEN 1 ELSE 0 END

    UNION ALL SELECT 11, N'dbo.pb_account_audit_events.MembershipId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'MembershipId') THEN 1 ELSE 0 END

    UNION ALL SELECT 12, N'dbo.pb_account_audit_events.CorrelationId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'CorrelationId') THEN 1 ELSE 0 END

    UNION ALL SELECT 13, N'dbo.pb_account_audit_events.Source exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'Source') THEN 1 ELSE 0 END

    UNION ALL SELECT 14, N'dbo.pb_account_audit_events.SafeSubject exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'SafeSubject') THEN 1 ELSE 0 END

    UNION ALL SELECT 15, N'dbo.pb_account_audit_events.SafeDetailJson exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'SafeDetailJson') THEN 1 ELSE 0 END

    UNION ALL SELECT 16, N'dbo.pb_account_audit_events.SchemaVersion exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AuditObjectId AND name COLLATE Latin1_General_BIN2 = N'SchemaVersion') THEN 1 ELSE 0 END

    UNION ALL SELECT 17, N'FK_pb_account_audit_events_ActorAccount exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_pb_account_audit_events_ActorAccount' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 18, N'FK_pb_account_audit_events_TargetAccount exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_pb_account_audit_events_TargetAccount' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 19, N'CK_pb_account_audit_events_EventName exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_audit_events_EventName' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 20, N'CK_pb_account_audit_events_Outcome exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_audit_events_Outcome' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 21, N'CK_pb_account_audit_events_ReasonCode exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_audit_events_ReasonCode' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 22, N'CK_pb_account_audit_events_Source exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_audit_events_Source' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 23, N'CK_pb_account_audit_events_SafeDetailJson exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_audit_events_SafeDetailJson' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 24, N'CK_pb_account_audit_events_SchemaVersion exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_audit_events_SchemaVersion' AND parent_object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 25, N'IX_pb_account_audit_events_CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_audit_events_CreatedAtUtc' AND object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 26, N'IX_pb_account_audit_events_EventName_CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_audit_events_EventName_CreatedAtUtc' AND object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 27, N'IX_pb_account_audit_events_ActorAccountId_CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_audit_events_ActorAccountId_CreatedAtUtc' AND object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 28, N'IX_pb_account_audit_events_TargetAccountId_CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_audit_events_TargetAccountId_CreatedAtUtc' AND object_id = @AuditObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 29, N'IX_pb_account_audit_events_CorrelationId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_audit_events_CorrelationId' AND object_id = @AuditObjectId) THEN 1 ELSE 0 END
)
SELECT
    @ScriptTitle AS ScriptTitle,
    item,
    expected_flag
FROM checks
ORDER BY sort_order;