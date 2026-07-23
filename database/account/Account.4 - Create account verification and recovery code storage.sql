/*
Title:
Account.4 - Create account verification and recovery code storage

Description:
Creates dbo.pb_account_codes in PluralBridgeChap2SafeSpine.
This table stores hashed verification and recovery codes for registration verification, username recovery, password reset, and contact verification.
The table stores only code hashes, never clear-text codes.
This script creates schema only. It does not insert, update, or delete account data.
*/

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 51000, 'Wrong database selected.', 1;
END;

IF OBJECT_ID(N'dbo.pb_accounts', N'U') IS NULL
BEGIN
    THROW 51001, 'dbo.pb_accounts does not exist.', 1;
END;

IF COL_LENGTH(N'dbo.pb_accounts', N'AccountId') IS NULL
BEGIN
    THROW 51002, 'dbo.pb_accounts.AccountId does not exist.', 1;
END;

IF OBJECT_ID(N'dbo.pb_account_codes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_account_codes
    (
        AccountCodeId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_pb_account_codes PRIMARY KEY
            DEFAULT NEWSEQUENTIALID(),

        AccountId UNIQUEIDENTIFIER NULL,

        CodePurpose NVARCHAR(50) NOT NULL,

        DestinationType NVARCHAR(50) NOT NULL,

        DestinationNormalized NVARCHAR(320) NOT NULL,

        CodeHash VARBINARY(256) NOT NULL,

        CodeHashAlgorithm NVARCHAR(50) NOT NULL,

        CodeHashVersion INT NOT NULL
            CONSTRAINT DF_pb_account_codes_CodeHashVersion DEFAULT (1),

        ExpiresAtUtc DATETIME2(3) NOT NULL,

        ConsumedAtUtc DATETIME2(3) NULL,

        AttemptCount INT NOT NULL
            CONSTRAINT DF_pb_account_codes_AttemptCount DEFAULT (0),

        MaxAttempts INT NOT NULL
            CONSTRAINT DF_pb_account_codes_MaxAttempts DEFAULT (5),

        CreatedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_pb_account_codes_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

        IssuedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_pb_account_codes_IssuedAtUtc DEFAULT SYSUTCDATETIME(),

        LastAttemptAtUtc DATETIME2(3) NULL,

        CorrelationId NVARCHAR(100) NOT NULL,

        CONSTRAINT FK_pb_account_codes_pb_accounts
            FOREIGN KEY (AccountId)
            REFERENCES dbo.pb_accounts(AccountId),

        CONSTRAINT CK_pb_account_codes_CodePurpose
            CHECK (CodePurpose IN
            (
                N'registration_verification',
                N'username_recovery',
                N'password_reset',
                N'contact_verification'
            )),

        CONSTRAINT CK_pb_account_codes_DestinationType
            CHECK (DestinationType IN
            (
                N'email'
            )),

        CONSTRAINT CK_pb_account_codes_AttemptCount
            CHECK (AttemptCount >= 0),

        CONSTRAINT CK_pb_account_codes_MaxAttempts
            CHECK (MaxAttempts > 0),

        CONSTRAINT CK_pb_account_codes_AttemptCountMaxAttempts
            CHECK (AttemptCount <= MaxAttempts)
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_codes_AccountId_CodePurpose'
      AND object_id = OBJECT_ID(N'dbo.pb_account_codes')
)
BEGIN
    CREATE INDEX IX_pb_account_codes_AccountId_CodePurpose
        ON dbo.pb_account_codes(AccountId, CodePurpose, ConsumedAtUtc, ExpiresAtUtc);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_codes_DestinationNormalized_CodePurpose'
      AND object_id = OBJECT_ID(N'dbo.pb_account_codes')
)
BEGIN
    CREATE INDEX IX_pb_account_codes_DestinationNormalized_CodePurpose
        ON dbo.pb_account_codes(DestinationNormalized, CodePurpose, CreatedAtUtc);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_pb_account_codes_CorrelationId'
      AND object_id = OBJECT_ID(N'dbo.pb_account_codes')
)
BEGIN
    CREATE INDEX IX_pb_account_codes_CorrelationId
        ON dbo.pb_account_codes(CorrelationId);
END;