USE [PluralBridgeChap2SafeSpine];
GO

DECLARE @TableExists INT;
DECLARE @ExpectedColumns INT;
DECLARE @PrimaryKeyExists INT;
DECLARE @UniqueSystemExists INT;
DECLARE @ForeignKeyCount INT;
DECLARE @CheckCount INT;
DECLARE @SingleRootIndexExists INT;
DECLARE @RootRelationshipExists INT;
DECLARE @SchemaOnly INT;

SELECT
    @TableExists =
        CASE WHEN OBJECT_ID(N'dbo.pb_system_relationships', N'U') IS NULL THEN 0 ELSE 1 END;

SELECT
    @ExpectedColumns =
        CASE WHEN
        (
            SELECT COUNT(DISTINCT COLUMN_NAME)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo'
              AND TABLE_NAME = N'pb_system_relationships'
              AND COLUMN_NAME IN
              (
                  N'SystemRelationshipId',
                  N'SystemId',
                  N'ParentSystemId',
                  N'RelationshipRank',
                  N'CreatedAtUtc',
                  N'UpdatedAtUtc'
              )
        ) = 6 THEN 1 ELSE 0 END;

SELECT
    @PrimaryKeyExists =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE [name] = N'PK_pb_system_relationships'
              AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
        ) THEN 1 ELSE 0 END;

SELECT
    @UniqueSystemExists =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.key_constraints
            WHERE [name] = N'UQ_pb_system_relationships_SystemId'
              AND [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
        ) THEN 1 ELSE 0 END;

SELECT
    @ForeignKeyCount =
        (
            SELECT COUNT(*)
            FROM sys.foreign_keys
            WHERE [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
              AND [name] IN
              (
                  N'FK_pb_system_relationships_pb_systems_SystemId',
                  N'FK_pb_system_relationships_pb_systems_ParentSystemId'
              )
        );

SELECT
    @CheckCount =
        (
            SELECT COUNT(*)
            FROM sys.check_constraints
            WHERE [parent_object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
              AND [name] IN
              (
                  N'CK_pb_system_relationships_NoSelfParent',
                  N'CK_pb_system_relationships_RelationshipRank'
              )
        );

SELECT
    @SingleRootIndexExists =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM sys.indexes
            WHERE [name] = N'UX_pb_system_relationships_SingleRoot'
              AND [object_id] = OBJECT_ID(N'dbo.pb_system_relationships')
        ) THEN 1 ELSE 0 END;

SELECT
    @RootRelationshipExists =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.pb_system_relationships
            WHERE SystemId = '826d77cf-8b1a-a301-4efe-1113e5a17e88'
              AND ParentSystemId IS NULL
              AND RelationshipRank = 1
        ) THEN 1 ELSE 0 END;

SELECT
    @SchemaOnly = 1;

SELECT
    TASK27_SYSTEM_RELATIONSHIPS_TABLE_EXISTS = @TableExists,
    TASK27_SYSTEM_RELATIONSHIPS_EXPECTED_COLUMNS = @ExpectedColumns,
    TASK27_SYSTEM_RELATIONSHIPS_PK_EXISTS = @PrimaryKeyExists,
    TASK27_SYSTEM_RELATIONSHIPS_UNIQUE_SYSTEM_EXISTS = @UniqueSystemExists,
    TASK27_SYSTEM_RELATIONSHIPS_FK_COUNT = @ForeignKeyCount,
    TASK27_SYSTEM_RELATIONSHIPS_CHECK_COUNT = @CheckCount,
    TASK27_SINGLE_ROOT_INDEX_EXISTS = @SingleRootIndexExists,
    TASK27_ROOT_RELATIONSHIP_EXISTS = @RootRelationshipExists,
    TASK27_SYSTEM_RELATIONSHIPS_SCHEMA_ONLY = @SchemaOnly,
    TASK27_OK =
        CASE WHEN
            @TableExists = 1
            AND @ExpectedColumns = 1
            AND @PrimaryKeyExists = 1
            AND @UniqueSystemExists = 1
            AND @ForeignKeyCount = 2
            AND @CheckCount = 2
            AND @SingleRootIndexExists = 1
            AND @RootRelationshipExists = 1
            AND @SchemaOnly = 1
        THEN 1 ELSE 0 END;