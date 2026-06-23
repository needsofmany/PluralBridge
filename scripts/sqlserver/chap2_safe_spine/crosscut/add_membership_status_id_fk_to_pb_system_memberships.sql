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

IF OBJECT_ID(N'dbo.pb_system_membership_statuses', N'U') IS NULL
BEGIN
    THROW 50002, 'Stop: dbo.pb_system_membership_statuses does not exist.', 1;
END;
GO

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_system_memberships sm
    LEFT JOIN dbo.pb_system_membership_statuses ms
        ON ms.MembershipStatusId = sm.MembershipStatusId
    WHERE ms.MembershipStatusId IS NULL
)
BEGIN
    THROW 50003, 'Stop: dbo.pb_system_memberships contains MembershipStatusId values with no matching lookup row.', 1;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_pb_system_memberships_pb_system_membership_statuses'
      AND parent_object_id = OBJECT_ID(N'dbo.pb_system_memberships')
)
BEGIN
    ALTER TABLE dbo.pb_system_memberships
        ADD CONSTRAINT FK_pb_system_memberships_pb_system_membership_statuses
        FOREIGN KEY (MembershipStatusId)
        REFERENCES dbo.pb_system_membership_statuses (MembershipStatusId);
END;
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
WHERE fk.name = N'FK_pb_system_memberships_pb_system_membership_statuses';
GO