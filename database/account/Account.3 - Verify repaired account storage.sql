/*
Title:
Account.3 - Verify repaired account storage

Description:
Read-only verification for PluralBridgeChap2SafeSpine after Account.3.
Confirms expected PascalCase account columns, credential table, foreign key, and indexes.
Also confirms the incorrectly added snake_case columns from the partial attempt are absent using exact binary column-name comparison.
This script does not change schema or data.
*/

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 51000, 'Wrong database selected.', 1;
END;

DECLARE @AccountsObjectId INT = OBJECT_ID(N'dbo.pb_accounts', N'U');
DECLARE @CredentialsObjectId INT = OBJECT_ID(N'dbo.pb_account_credentials', N'U');

WITH checks AS
(
    SELECT
        1 AS sort_order,
        N'current database is PluralBridgeChap2SafeSpine' AS item,
        CASE WHEN DB_NAME() = N'PluralBridgeChap2SafeSpine' THEN 1 ELSE 0 END AS expected_flag

    UNION ALL SELECT 2, N'dbo.pb_accounts exists',
        CASE WHEN @AccountsObjectId IS NOT NULL THEN 1 ELSE 0 END

    UNION ALL SELECT 3, N'dbo.pb_accounts.AccountId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'AccountId') THEN 1 ELSE 0 END

    UNION ALL SELECT 4, N'dbo.pb_accounts.Email exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'Email') THEN 1 ELSE 0 END

    UNION ALL SELECT 5, N'dbo.pb_accounts.DisplayName exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'DisplayName') THEN 1 ELSE 0 END

    UNION ALL SELECT 6, N'dbo.pb_accounts.AccountStatusId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'AccountStatusId') THEN 1 ELSE 0 END

    UNION ALL SELECT 7, N'dbo.pb_accounts.CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'CreatedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 8, N'dbo.pb_accounts.UpdatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'UpdatedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 9, N'dbo.pb_accounts.Username exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'Username') THEN 1 ELSE 0 END

    UNION ALL SELECT 10, N'dbo.pb_accounts.NormalizedUsername exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'NormalizedUsername') THEN 1 ELSE 0 END

    UNION ALL SELECT 11, N'dbo.pb_accounts.NormalizedEmail exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'NormalizedEmail') THEN 1 ELSE 0 END

    UNION ALL SELECT 12, N'dbo.pb_accounts.IsEmailVerified exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'IsEmailVerified') THEN 1 ELSE 0 END

    UNION ALL SELECT 13, N'dbo.pb_accounts.LastLoginAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'LastLoginAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 14, N'dbo.pb_account_credentials exists',
        CASE WHEN @CredentialsObjectId IS NOT NULL THEN 1 ELSE 0 END

    UNION ALL SELECT 15, N'dbo.pb_account_credentials.AccountCredentialId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'AccountCredentialId') THEN 1 ELSE 0 END

    UNION ALL SELECT 16, N'dbo.pb_account_credentials.AccountId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'AccountId') THEN 1 ELSE 0 END

    UNION ALL SELECT 17, N'dbo.pb_account_credentials.PasswordHash exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'PasswordHash') THEN 1 ELSE 0 END

    UNION ALL SELECT 18, N'dbo.pb_account_credentials.PasswordHashAlgorithm exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'PasswordHashAlgorithm') THEN 1 ELSE 0 END

    UNION ALL SELECT 19, N'dbo.pb_account_credentials.PasswordHashVersion exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'PasswordHashVersion') THEN 1 ELSE 0 END

    UNION ALL SELECT 20, N'dbo.pb_account_credentials.PasswordChangedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'PasswordChangedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 21, N'dbo.pb_account_credentials.CreatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'CreatedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 22, N'dbo.pb_account_credentials.UpdatedAtUtc exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @CredentialsObjectId AND name COLLATE Latin1_General_BIN2 = N'UpdatedAtUtc') THEN 1 ELSE 0 END

    UNION ALL SELECT 23, N'FK_pb_account_credentials_pb_accounts exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_pb_account_credentials_pb_accounts' AND parent_object_id = @CredentialsObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 24, N'UX_pb_accounts_NormalizedUsername exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pb_accounts_NormalizedUsername' AND object_id = @AccountsObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 25, N'UX_pb_accounts_NormalizedEmail exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pb_accounts_NormalizedEmail' AND object_id = @AccountsObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 26, N'UX_pb_account_credentials_AccountId exists',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_pb_account_credentials_AccountId' AND object_id = @CredentialsObjectId) THEN 1 ELSE 0 END

    UNION ALL SELECT 27, N'snake_case username absent',
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'username') THEN 1 ELSE 0 END

    UNION ALL SELECT 28, N'snake_case display_name absent',
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'display_name') THEN 1 ELSE 0 END

    UNION ALL SELECT 29, N'snake_case contact_email absent',
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'contact_email') THEN 1 ELSE 0 END

    UNION ALL SELECT 30, N'snake_case contact_email_normalized absent',
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'contact_email_normalized') THEN 1 ELSE 0 END

    UNION ALL SELECT 31, N'snake_case is_contact_email_verified absent',
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'is_contact_email_verified') THEN 1 ELSE 0 END

    UNION ALL SELECT 32, N'snake_case last_login_utc absent',
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'last_login_utc') THEN 1 ELSE 0 END

    UNION ALL SELECT 33, N'snake_case updated_utc absent',
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @AccountsObjectId AND name COLLATE Latin1_General_BIN2 = N'updated_utc') THEN 1 ELSE 0 END
)
SELECT
    item,
    expected_flag
FROM checks
ORDER BY sort_order;