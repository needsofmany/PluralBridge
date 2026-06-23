SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;

DECLARE @DemoAccountId uniqueidentifier = '8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001';
DECLARE @DemoSystemMembershipId uniqueidentifier = '7f7f0d8c-08d4-42df-9f0a-8db13d2d0009';

SELECT
    a.AccountId,
    a.Email,
    a.DisplayName,
    sm.SystemMembershipId,
    sm.SystemId,
    ms.StatusName AS MembershipStatusName,
    r.RoleId,
    r.RoleName
FROM dbo.pb_accounts a
JOIN dbo.pb_system_memberships sm
    ON sm.AccountId = a.AccountId
JOIN dbo.pb_system_membership_statuses ms
    ON ms.MembershipStatusId = sm.MembershipStatusId
JOIN dbo.pb_system_membership_roles smr
    ON smr.SystemMembershipId = sm.SystemMembershipId
JOIN dbo.pb_roles r
    ON r.RoleId = smr.RoleId
WHERE a.AccountId = @DemoAccountId
  AND sm.SystemMembershipId = @DemoSystemMembershipId
ORDER BY r.DisplayOrder;

SELECT
    COUNT(*) AS DemoOwnerMembershipPathCount
FROM dbo.pb_accounts a
JOIN dbo.pb_system_memberships sm
    ON sm.AccountId = a.AccountId
JOIN dbo.pb_system_membership_statuses ms
    ON ms.MembershipStatusId = sm.MembershipStatusId
JOIN dbo.pb_system_membership_roles smr
    ON smr.SystemMembershipId = sm.SystemMembershipId
JOIN dbo.pb_roles r
    ON r.RoleId = smr.RoleId
WHERE a.AccountId = @DemoAccountId
  AND a.Email = N'demo@thepluralbridge.local'
  AND sm.SystemMembershipId = @DemoSystemMembershipId
  AND ms.StatusName = N'Active'
  AND r.RoleName = N'Owner';
GO