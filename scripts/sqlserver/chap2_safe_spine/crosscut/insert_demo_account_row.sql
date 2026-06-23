SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_accounts
    WHERE AccountId = '8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001'
       OR Email = N'demo@thepluralbridge.local'
)
BEGIN
    THROW 50001, 'Stop: demo account already exists by AccountId or Email.', 1;
END;
GO

INSERT INTO dbo.pb_accounts
(
    AccountId,
    Email,
    DisplayName,
    AccountStatusId,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    '8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001',
    N'demo@thepluralbridge.local',
    N'PluralBridge Demo Account',
    s.AccountStatusId,
    sysutcdatetime(),
    NULL
FROM dbo.pb_account_statuses s
WHERE s.StatusName = N'Active';
GO

IF @@ROWCOUNT <> 1
BEGIN
    THROW 50002, 'Stop: demo account insert did not insert exactly one row.', 1;
END;
GO

SELECT
    a.AccountId,
    a.Email,
    a.DisplayName,
    a.AccountStatusId,
    s.StatusName,
    a.CreatedAtUtc,
    a.UpdatedAtUtc
FROM dbo.pb_accounts a
JOIN dbo.pb_account_statuses s
    ON s.AccountStatusId = a.AccountStatusId
WHERE a.AccountId = '8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001';
GO