/*
Title:
Account.3 - Repair partial account storage and complete credential table

Description:
Repairs the partial Account.3 schema attempt in PluralBridgeChap2SafeSpine by removing the incorrectly named snake_case columns added to dbo.pb_accounts, adding the PascalCase account columns that match the existing database naming style, creating dbo.pb_account_credentials with a foreign key to dbo.pb_accounts(AccountId), and adding the required unique indexes for normalized username, normalized email, and one credential row per account.

This is schema-only. It does not delete account rows, system rows, membership rows, role rows, member rows, import rows, or source records.
*/

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 51000, 'Wrong database selected.', 1;
END;

IF OBJECT_ID(N'dbo.pb_accounts', N'U') IS NULL
BEGIN
    THROW 51001, 'dbo.pb_accounts does not exist.', 1;
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'updated_utc') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts DROP COLUMN updated_utc;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'last_login_utc') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts DROP COLUMN last_login_utc;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'is_contact_email_verified') IS NOT NULL
BEGIN
    DECLARE @DropDefaultSql NVARCHAR(MAX);
    DECLARE @DefaultConstraintName SYSNAME;

    SELECT @DefaultConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.pb_accounts')
      AND c.name = N'is_contact_email_verified';

    IF @DefaultConstraintName IS NOT NULL
    BEGIN
        SET @DropDefaultSql = N'ALTER TABLE dbo.pb_accounts DROP CONSTRAINT ' + QUOTENAME(@DefaultConstraintName) + N';';
        EXEC(@DropDefaultSql);
    END;

    EXEC(N'ALTER TABLE dbo.pb_accounts DROP COLUMN is_contact_email_verified;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'contact_email_normalized') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts DROP COLUMN contact_email_normalized;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'contact_email') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts DROP COLUMN contact_email;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'display_name') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts DROP COLUMN display_name;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'username') IS NOT NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts DROP COLUMN username;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'Username') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts ADD Username NVARCHAR(100) NULL;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'NormalizedUsername') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts ADD NormalizedUsername NVARCHAR(100) NULL;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'NormalizedEmail') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts ADD NormalizedEmail NVARCHAR(320) NULL;');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'IsEmailVerified') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts ADD IsEmailVerified BIT NOT NULL CONSTRAINT DF_pb_accounts_IsEmailVerified DEFAULT (0);');
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'LastLoginAtUtc') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.pb_accounts ADD LastLoginAtUtc DATETIME2(3) NULL;');
END;

IF OBJECT_ID(N'dbo.pb_account_credentials', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_account_credentials
    (
        AccountCredentialId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_pb_account_credentials PRIMARY KEY
            DEFAULT NEWSEQUENTIALID(),

        AccountId UNIQUEIDENTIFIER NOT NULL,

        PasswordHash VARBINARY(256) NOT NULL,

        PasswordHashAlgorithm NVARCHAR(50) NOT NULL,

        PasswordHashVersion INT NOT NULL
            CONSTRAINT DF_pb_account_credentials_PasswordHashVersion DEFAULT (1),

        PasswordChangedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_pb_account_credentials_PasswordChangedAtUtc DEFAULT SYSUTCDATETIME(),

        CreatedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_pb_account_credentials_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

        UpdatedAtUtc DATETIME2(3) NULL,

        CONSTRAINT FK_pb_account_credentials_pb_accounts
            FOREIGN KEY (AccountId)
            REFERENCES dbo.pb_accounts(AccountId)
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_pb_accounts_NormalizedUsername'
      AND object_id = OBJECT_ID(N'dbo.pb_accounts')
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UX_pb_accounts_NormalizedUsername ON dbo.pb_accounts(NormalizedUsername) WHERE NormalizedUsername IS NOT NULL;');
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_pb_accounts_NormalizedEmail'
      AND object_id = OBJECT_ID(N'dbo.pb_accounts')
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UX_pb_accounts_NormalizedEmail ON dbo.pb_accounts(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;');
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_pb_account_credentials_AccountId'
      AND object_id = OBJECT_ID(N'dbo.pb_account_credentials')
)
BEGIN
    CREATE UNIQUE INDEX UX_pb_account_credentials_AccountId
        ON dbo.pb_account_credentials(AccountId);
END;