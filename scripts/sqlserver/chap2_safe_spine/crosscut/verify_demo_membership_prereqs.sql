SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;

DECLARE @DemoAccountId uniqueidentifier = '8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001';
DECLARE @DemoSystemId uniqueidentifier;
DECLARE @SystemCount int;

SELECT @SystemCount = COUNT(*)
FROM dbo.pb_systems;

IF @SystemCount <> 1
BEGIN
    THROW 50001, 'Stop: expected exactly one demo system row in dbo.pb_systems.', 1;
END;

SELECT @DemoSystemId = SystemId
FROM dbo.pb_systems;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_accounts
    WHERE AccountId = @DemoAccountId
      AND Email = N'demo@thepluralbridge.local'
)
BEGIN
    THROW 50002, 'Stop: demo account row does not exist.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_system_membership_statuses
    WHERE StatusName = N'Active'
)
BEGIN
    THROW 50003, 'Stop: Active membership status does not exist.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_roles
    WHERE RoleName = N'Owner'
)
BEGIN
    THROW 50004, 'Stop: Owner role does not exist.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_systems
    WHERE SystemId = @DemoSystemId
)
BEGIN
    THROW 50005, 'Stop: target demo system row does not exist.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_system_memberships
    WHERE AccountId = @DemoAccountId
      AND SystemId = @DemoSystemId
)
BEGIN
    THROW 50006, 'Stop: demo account already has a membership row for the demo system.', 1;
END;

SELECT
    a.AccountId,
    a.Email,
    a.DisplayName,
    s.SystemId,
    ms.MembershipStatusId,
    ms.StatusName AS MembershipStatusName,
    r.RoleId,
    r.RoleName
FROM dbo.pb_accounts a
CROSS JOIN dbo.pb_systems s
CROSS JOIN dbo.pb_system_membership_statuses ms
CROSS JOIN dbo.pb_roles r
WHERE a.AccountId = @DemoAccountId
  AND s.SystemId = @DemoSystemId
  AND ms.StatusName = N'Active'
  AND r.RoleName = N'Owner';

SELECT
    COUNT(*) AS ExistingDemoMembershipRows
FROM dbo.pb_system_memberships
WHERE AccountId = @DemoAccountId
  AND SystemId = @DemoSystemId;
GO
