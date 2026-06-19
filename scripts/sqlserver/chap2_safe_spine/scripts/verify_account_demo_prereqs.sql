SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_accounts', N'U') IS NULL
BEGIN
    THROW 50001, 'Stop: dbo.pb_accounts does not exist.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_account_statuses', N'U') IS NULL
BEGIN
    THROW 50002, 'Stop: dbo.pb_account_statuses does not exist.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_account_statuses
    WHERE StatusName = N'Active'
)
BEGIN
    THROW 50003, 'Stop: Active account status does not exist.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_accounts
    WHERE Email = N'demo@thepluralbridge.local'
)
BEGIN
    THROW 50004, 'Stop: demo account email already exists in dbo.pb_accounts.', 1;
END;
GO

SELECT
    s.AccountStatusId,
    s.StatusName,
    s.StatusDesc,
    s.DisplayOrder,
    s.IsActive
FROM dbo.pb_account_statuses s
WHERE s.StatusName = N'Active';
GO

SELECT
    COUNT(*) AS ExistingDemoAccountRows
FROM dbo.pb_accounts
WHERE Email = N'demo@thepluralbridge.local';
GO