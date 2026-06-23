USE [PluralBridgeChap2SafeSpine];
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE [name] = N'PK_pb_system_relationships'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
)
BEGIN
    ALTER TABLE dbo.pb_system_relationships
    ADD CONSTRAINT PK_pb_system_relationships
        PRIMARY KEY CLUSTERED (SystemRelationshipId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE [name] = N'UQ_pb_system_relationships_SystemId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
)
BEGIN
    ALTER TABLE dbo.pb_system_relationships
    ADD CONSTRAINT UQ_pb_system_relationships_SystemId
        UNIQUE (SystemId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_pb_system_relationships_pb_systems_SystemId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
)
BEGIN
    ALTER TABLE dbo.pb_system_relationships
    ADD CONSTRAINT FK_pb_system_relationships_pb_systems_SystemId
        FOREIGN KEY (SystemId)
        REFERENCES dbo.pb_systems (SystemId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_pb_system_relationships_pb_systems_ParentSystemId'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
)
BEGIN
    ALTER TABLE dbo.pb_system_relationships
    ADD CONSTRAINT FK_pb_system_relationships_pb_systems_ParentSystemId
        FOREIGN KEY (ParentSystemId)
        REFERENCES dbo.pb_systems (SystemId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = N'CK_pb_system_relationships_NoSelfParent'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
)
BEGIN
    ALTER TABLE dbo.pb_system_relationships
    ADD CONSTRAINT CK_pb_system_relationships_NoSelfParent
        CHECK (ParentSystemId IS NULL OR ParentSystemId <> SystemId);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE [name] = N'CK_pb_system_relationships_RelationshipRank'
      AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
)
BEGIN
    ALTER TABLE dbo.pb_system_relationships
    ADD CONSTRAINT CK_pb_system_relationships_RelationshipRank
        CHECK (RelationshipRank >= 1);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_pb_system_relationships_SingleRoot'
      AND [object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
)
BEGIN
    CREATE UNIQUE INDEX UX_pb_system_relationships_SingleRoot
        ON dbo.pb_system_relationships (ParentSystemId)
        WHERE ParentSystemId IS NULL;
END;
GO

SELECT
    TASK27_SYSTEM_RELATIONSHIPS_PK_EXISTS =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE [name] = N'PK_pb_system_relationships'
              AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
        ) THEN 1 ELSE 0 END,
    TASK27_SYSTEM_RELATIONSHIPS_UNIQUE_SYSTEM_EXISTS =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE [name] = N'UQ_pb_system_relationships_SystemId'
              AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
        ) THEN 1 ELSE 0 END,
    TASK27_SYSTEM_RELATIONSHIPS_FK_COUNT =
        (
            SELECT COUNT(*)
            FROM sys.foreign_keys
            WHERE [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
              AND [name] IN
              (
                  N'FK_pb_system_relationships_pb_systems_SystemId',
                  N'FK_pb_system_relationships_pb_systems_ParentSystemId'
              )
        ),
    TASK27_SYSTEM_RELATIONSHIPS_CHECK_COUNT =
        (
            SELECT COUNT(*)
            FROM sys.check_constraints
            WHERE [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
              AND [name] IN
              (
                  N'CK_pb_system_relationships_NoSelfParent',
                  N'CK_pb_system_relationships_RelationshipRank'
              )
        ),
    TASK27_SINGLE_ROOT_INDEX_EXISTS =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE [name] = N'UX_pb_system_relationships_SingleRoot'
              AND [object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
        ) THEN 1 ELSE 0 END;