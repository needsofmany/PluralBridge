-- Record of the object-membership role migration already executed on
-- PluralBridgeChap2SafeSpine. Do not execute this script again.
-- Executed result: 21 existing system Owner assignments were copied.
-- 23 system memberships matched an object membership for the same account and system.

SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_object_membership_roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_object_membership_roles
    (
        ObjectMembershipId uniqueidentifier NOT NULL,
        RoleId int NOT NULL,
        CreatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_pb_object_membership_roles_CreatedAtUtc DEFAULT sysutcdatetime(),

        CONSTRAINT PK_pb_object_membership_roles
            PRIMARY KEY CLUSTERED (ObjectMembershipId, RoleId),

        CONSTRAINT FK_pb_object_membership_roles_pb_object_memberships
            FOREIGN KEY (ObjectMembershipId)
            REFERENCES dbo.pb_object_memberships (ObjectMembershipId),

        CONSTRAINT FK_pb_object_membership_roles_pb_roles
            FOREIGN KEY (RoleId)
            REFERENCES dbo.pb_roles (RoleId)
    );

    CREATE INDEX IX_pb_object_membership_roles_RoleId
        ON dbo.pb_object_membership_roles (RoleId);
END;
GO

INSERT INTO dbo.pb_object_membership_roles
(
    ObjectMembershipId,
    RoleId
)
SELECT
    om.ObjectMembershipId,
    mr.RoleId
FROM dbo.pb_system_membership_roles AS mr
INNER JOIN dbo.pb_system_memberships AS sm
    ON sm.SystemMembershipId = mr.SystemMembershipId
INNER JOIN dbo.pb_roles AS r
    ON r.RoleId = mr.RoleId
   AND r.RoleName = N'Owner'
INNER JOIN dbo.pb_object_memberships AS om
    ON om.AccountId = sm.AccountId
   AND om.ObjectType = N'system'
   AND TRY_CONVERT(uniqueidentifier, om.ObjectId) = sm.SystemId
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_object_membership_roles AS existing
    WHERE existing.ObjectMembershipId = om.ObjectMembershipId
      AND existing.RoleId = mr.RoleId
);
GO
