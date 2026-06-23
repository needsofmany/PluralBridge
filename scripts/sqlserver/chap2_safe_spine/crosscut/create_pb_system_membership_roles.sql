SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_system_memberships', N'U') IS NULL
BEGIN
    THROW 50001, 'Stop: dbo.pb_system_memberships does not exist.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_roles', N'U') IS NULL
BEGIN
    THROW 50002, 'Stop: dbo.pb_roles does not exist.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_system_membership_roles', N'U') IS NOT NULL
BEGIN
    THROW 50003, 'Stop: dbo.pb_system_membership_roles already exists.', 1;
END;
GO

CREATE TABLE dbo.pb_system_membership_roles
(
    SystemMembershipId uniqueidentifier NOT NULL,
    RoleId int NOT NULL,

    CreatedAtUtc datetime2(3) NOT NULL
        CONSTRAINT DF_pb_system_membership_roles_CreatedAtUtc DEFAULT sysutcdatetime(),

    CONSTRAINT PK_pb_system_membership_roles
        PRIMARY KEY CLUSTERED (SystemMembershipId, RoleId),

    CONSTRAINT FK_pb_system_membership_roles_pb_system_memberships
        FOREIGN KEY (SystemMembershipId)
        REFERENCES dbo.pb_system_memberships (SystemMembershipId),

    CONSTRAINT FK_pb_system_membership_roles_pb_roles
        FOREIGN KEY (RoleId)
        REFERENCES dbo.pb_roles (RoleId)
);
GO

CREATE INDEX IX_pb_system_membership_roles_RoleId
    ON dbo.pb_system_membership_roles (RoleId);
GO

SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc
FROM sys.objects o
JOIN sys.schemas s
    ON s.schema_id = o.schema_id
WHERE o.object_id = OBJECT_ID(N'dbo.pb_system_membership_roles');
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
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.pb_system_membership_roles')
ORDER BY fk.name;
GO