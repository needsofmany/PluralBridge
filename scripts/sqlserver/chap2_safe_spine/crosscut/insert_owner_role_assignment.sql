SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

DECLARE @DemoSystemMembershipId uniqueidentifier = '7f7f0d8c-08d4-42df-9f0a-8db13d2d0009';
DECLARE @OwnerRoleId int;
DECLARE @InsertedRowCount int;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_system_memberships
    WHERE SystemMembershipId = @DemoSystemMembershipId
)
BEGIN
    THROW 50001, 'Stop: demo system membership row does not exist.', 1;
END;

SELECT @OwnerRoleId = RoleId
FROM dbo.pb_roles
WHERE RoleName = N'Owner';

IF @OwnerRoleId IS NULL
BEGIN
    THROW 50002, 'Stop: Owner role does not exist.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_system_membership_roles
    WHERE SystemMembershipId = @DemoSystemMembershipId
      AND RoleId = @OwnerRoleId
)
BEGIN
    THROW 50003, 'Stop: demo system membership already has Owner role.', 1;
END;

INSERT INTO dbo.pb_system_membership_roles
(
    SystemMembershipId,
    RoleId,
    CreatedAtUtc
)
VALUES
(
    @DemoSystemMembershipId,
    @OwnerRoleId,
    sysutcdatetime()
);

SET @InsertedRowCount = @@ROWCOUNT;

IF @InsertedRowCount <> 1
BEGIN
    THROW 50004, 'Stop: Owner role assignment insert did not insert exactly one row.', 1;
END;

SELECT
    smr.SystemMembershipId,
    smr.RoleId,
    r.RoleName,
    smr.CreatedAtUtc
FROM dbo.pb_system_membership_roles smr
JOIN dbo.pb_roles r
    ON r.RoleId = smr.RoleId
WHERE smr.SystemMembershipId = @DemoSystemMembershipId
ORDER BY r.DisplayOrder;
GO