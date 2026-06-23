SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

DECLARE @DemoAccountId uniqueidentifier = '8f3f8e4b-0d64-4b4a-9f6e-8db13d2d0001';
DECLARE @DemoSystemMembershipId uniqueidentifier = '7f7f0d8c-08d4-42df-9f0a-8db13d2d0009';
DECLARE @DemoSystemId uniqueidentifier;
DECLARE @ActiveMembershipStatusId int;
DECLARE @SystemCount int;
DECLARE @InsertedRowCount int;

SELECT @SystemCount = COUNT(*)
FROM dbo.pb_systems;

IF @SystemCount <> 1
BEGIN
    THROW 50001, 'Stop: expected exactly one demo system row in dbo.pb_systems.', 1;
END;

SELECT @DemoSystemId = SystemId
FROM dbo.pb_systems;

SELECT @ActiveMembershipStatusId = MembershipStatusId
FROM dbo.pb_system_membership_statuses
WHERE StatusName = N'Active';

IF @ActiveMembershipStatusId IS NULL
BEGIN
    THROW 50002, 'Stop: Active membership status does not exist.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_system_memberships
    WHERE SystemMembershipId = @DemoSystemMembershipId
)
BEGIN
    THROW 50003, 'Stop: demo SystemMembershipId already exists.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_system_memberships
    WHERE AccountId = @DemoAccountId
      AND SystemId = @DemoSystemId
)
BEGIN
    THROW 50004, 'Stop: demo account already has a membership row for the demo system.', 1;
END;

INSERT INTO dbo.pb_system_memberships
(
    SystemMembershipId,
    AccountId,
    SystemId,
    MembershipStatusId,
    CreatedAtUtc,
    UpdatedAtUtc
)
VALUES
(
    @DemoSystemMembershipId,
    @DemoAccountId,
    @DemoSystemId,
    @ActiveMembershipStatusId,
    sysutcdatetime(),
    NULL
);

SET @InsertedRowCount = @@ROWCOUNT;

IF @InsertedRowCount <> 1
BEGIN
    THROW 50005, 'Stop: demo membership insert did not insert exactly one row.', 1;
END;

SELECT
    sm.SystemMembershipId,
    sm.AccountId,
    a.Email,
    sm.SystemId,
    sm.MembershipStatusId,
    ms.StatusName AS MembershipStatusName,
    sm.CreatedAtUtc,
    sm.UpdatedAtUtc
FROM dbo.pb_system_memberships sm
JOIN dbo.pb_accounts a
    ON a.AccountId = sm.AccountId
JOIN dbo.pb_system_membership_statuses ms
    ON ms.MembershipStatusId = sm.MembershipStatusId
WHERE sm.SystemMembershipId = @DemoSystemMembershipId;
GO