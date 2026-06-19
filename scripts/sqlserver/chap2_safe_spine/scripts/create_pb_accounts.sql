SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_account_statuses', N'U') IS NULL
BEGIN
    THROW 50001, 'Stop: dbo.pb_account_statuses does not exist.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_accounts', N'U') IS NOT NULL
BEGIN
    THROW 50002, 'Stop: dbo.pb_accounts already exists.', 1;
END;
GO

CREATE TABLE dbo.pb_accounts
(
    AccountId uniqueidentifier NOT NULL
        CONSTRAINT DF_pb_accounts_AccountId DEFAULT newsequentialid(),

    Email nvarchar(320) NOT NULL,
    DisplayName nvarchar(255) NULL,
    AccountStatusId int NOT NULL,

    CreatedAtUtc datetime2(3) NOT NULL
        CONSTRAINT DF_pb_accounts_CreatedAtUtc DEFAULT sysutcdatetime(),

    UpdatedAtUtc datetime2(3) NULL,

    CONSTRAINT PK_pb_accounts
        PRIMARY KEY CLUSTERED (AccountId),

    CONSTRAINT UQ_pb_accounts_Email
        UNIQUE (Email),

    CONSTRAINT FK_pb_accounts_pb_account_statuses
        FOREIGN KEY (AccountStatusId)
        REFERENCES dbo.pb_account_statuses (AccountStatusId),

    CONSTRAINT CK_pb_accounts_Email_NotBlank
        CHECK (len(ltrim(rtrim(Email))) > 0)
);
GO

CREATE INDEX IX_pb_accounts_AccountStatusId
    ON dbo.pb_accounts (AccountStatusId);
GO

SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length,
    c.is_nullable
FROM sys.columns c
JOIN sys.types t
    ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.pb_accounts')
ORDER BY c.column_id;
GO

SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    pc.name AS ParentColumn,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    rc.name AS ReferencedColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc
    ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns pc
    ON pc.object_id = fkc.parent_object_id
   AND pc.column_id = fkc.parent_column_id
JOIN sys.columns rc
    ON rc.object_id = fkc.referenced_object_id
   AND rc.column_id = fkc.referenced_column_id
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.pb_accounts')
ORDER BY fk.name;
GO