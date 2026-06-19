SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_system_memberships', N'U') IS NOT NULL
BEGIN
    THROW 50001, 'Stop: dbo.pb_system_memberships already exists.', 1;
END;
GO

CREATE TABLE dbo.pb_system_memberships
(
    SystemMembershipId uniqueidentifier NOT NULL
        CONSTRAINT DF_pb_system_memberships_SystemMembershipId DEFAULT newsequentialid(),

    AccountId uniqueidentifier NOT NULL,
    SystemId uniqueidentifier NOT NULL,
    RoleId int NOT NULL,
    MembershipStatusId int NOT NULL,

    CreatedAtUtc datetime2(3) NOT NULL
        CONSTRAINT DF_pb_system_memberships_CreatedAtUtc DEFAULT sysutcdatetime(),

    UpdatedAtUtc datetime2(3) NULL,

    CONSTRAINT PK_pb_system_memberships
        PRIMARY KEY CLUSTERED (SystemMembershipId),

    CONSTRAINT FK_pb_system_memberships_pb_systems
        FOREIGN KEY (SystemId)
        REFERENCES dbo.pb_systems (SystemId),

    CONSTRAINT FK_pb_system_memberships_pb_roles
        FOREIGN KEY (RoleId)
        REFERENCES dbo.pb_roles (RoleId)
);
GO

CREATE INDEX IX_pb_system_memberships_AccountId
    ON dbo.pb_system_memberships (AccountId);
GO

CREATE INDEX IX_pb_system_memberships_SystemId
    ON dbo.pb_system_memberships (SystemId);
GO

CREATE INDEX IX_pb_system_memberships_RoleId
    ON dbo.pb_system_memberships (RoleId);
GO

SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length,
    c.is_nullable
FROM sys.columns c
JOIN sys.types t
    ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.pb_system_memberships')
ORDER BY c.column_id;
GO

SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable
FROM sys.foreign_keys fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.pb_system_memberships')
ORDER BY fk.name;
GO