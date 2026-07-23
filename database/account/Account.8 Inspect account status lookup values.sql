DECLARE @ScriptTitle NVARCHAR(200) = N'Account.8 Inspect account status lookup values';

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    DECLARE @WrongDatabaseMessage NVARCHAR(400) = @ScriptTitle + N': wrong database selected.';
    THROW 51000, @WrongDatabaseMessage, 1;
END;

SELECT
    @ScriptTitle AS ScriptTitle,
    AccountStatusId,
    StatusName,
    StatusDesc,
    DisplayOrder,
    IsActive
FROM dbo.pb_account_statuses
ORDER BY
    DisplayOrder,
    AccountStatusId;