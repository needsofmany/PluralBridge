/*
Title:
Account.4 - Verify account verification and recovery code storage

Description:
Read-only verification for dbo.pb_account_codes in PluralBridgeChap2SafeSpine.
Confirms the table, expected columns, foreign key, check constraints, and indexes exist.
This script does not change schema or data.
*/

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 51000, 'Wrong database selected.', 1;
END;

DECLARE @CodesObjectId INT = OBJECT_ID(N'dbo.pb_account_codes', N'U');

WITH checks AS
(
    SELECT 1 AS sort_order, N'current database is PluralBridgeChap2SafeSpine' AS item,
        CASE WHEN DB_NAME() = N'PluralBridgeChap2SafeSpine' THEN 1 ELSE 0 END AS expected_flag

    UNION ALL SELECT 2, N'dbo.pb_account_codes exists',
        CASE WHEN @CodesObjectId IS NOT NULL THEN 1 ELSE 0 END

    UNION ALL SELECT 3, N'dbo.pb_account_codes.AccountCodeId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'AccountCodeId') THEN 1 ELSE 0 END

    UNION ALL SELECT 4, N'dbo.pb_account_codes.AccountId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'AccountId') THEN 1 ELSE 0 END

    UNION ALL SELECT 5, N'dbo.pb_account_codes.CodePurpose exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'CodePurpose') THEN 1 ELSE 0 END

    UNION ALL SELECT 6, N'dbo.pb_account_codes.DestinationType exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'DestinationType') THEN 1 ELSE 0 END

    UNION ALL SELECT 7, N'dbo.pb_account_codes.DestinationNormalized exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'DestinationNormalized') THEN 1 ELSE 0 END

    UNION ALL SELECT 8, N'dbo.pb_account_codes.CodeHash exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'CodeHash') THEN 1 ELSE 0 END

    UNION ALL SELECT 9, N'dbo.pb_account_codes.CodeHashAlgorithm exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'CodeHashAlgorithm') THEN 1 ELSE 0 END

    UNION ALL SELECT 10, N'dbo.pb_account_codes.CodeHashVersion exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'CodeHashVersion') THEN 1 ELSE 0 END

    UNION ALL SELECT 11, N'dbo.pb_account_codes.ExpiresAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'ExpiresAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 12, N'dbo.pb_account_codes.ConsumedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'ConsumedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 13, N'dbo.pb_account_codes.AttemptCount exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'AttemptCount') THEN 1 ELSE 0 END

    UNION ALL SELECT 14, N'dbo.pb_account_codes.MaxAttempts exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'MaxAttempts') THEN 1 ELSE 0 END

    UNION ALL SELECT 15, N'dbo.pb_account_codes.CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'CreatedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 16, N'dbo.pb_account_codes.IssuedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'IssuedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 17, N'dbo.pb_account_codes.LastAttemptAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'LastAttemptAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 18, N'dbo.pb_account_codes.CorrelationId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CodesObjectId AND name = N'CorrelationId') THEN 1 ELSE 0 END

    UNION ALL SELECT 19, N'FK_pb_account_codes_pb_accounts exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_pb_account_codes_pb_accounts' AND parent_object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 20, N'CK_pb_account_codes_CodePurpose exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_codes_CodePurpose' AND parent_object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 21, N'CK_pb_account_codes_DestinationType exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_codes_DestinationType' AND parent_object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 22, N'CK_pb_account_codes_AttemptCount exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_codes_AttemptCount' AND parent_object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 23, N'CK_pb_account_codes_MaxAttempts exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_codes_MaxAttempts' AND parent_object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 24, N'CK_pb_account_codes_AttemptCountMaxAttempts exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_pb_account_codes_AttemptCountMaxAttempts' AND parent_object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 25, N'IX_pb_account_codes_AccountId_CodePurpose exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_codes_AccountId_CodePurpose' AND object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 26, N'IX_pb_account_codes_DestinationNormalized_CodePurpose exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_codes_DestinationNormalized_CodePurpose' AND object_id = @CodesObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 27, N'IX_pb_account_codes_CorrelationId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_pb_account_codes_CorrelationId' AND object_id = @CodesObjectId) THEN 1 ELSE 0 END
)
SELECT
    item,
    expected_flag
FROM checks
ORDER BY sort_order;