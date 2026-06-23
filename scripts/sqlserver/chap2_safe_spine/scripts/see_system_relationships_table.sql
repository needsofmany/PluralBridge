USE [PluralBridgeChap2SafeSpine];
GO

DECLARE @RootSystemId UNIQUEIDENTIFIER;

SELECT @RootSystemId = SystemId
FROM dbo.pb_systems
WHERE SystemId = '826d77cf-8b1a-a301-4efe-1113e5a17e88';

IF @RootSystemId IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM dbo.pb_system_relationships
       WHERE SystemId = @RootSystemId
   )
BEGIN
    INSERT INTO dbo.pb_system_relationships
    (
        SystemRelationshipId,
        SystemId,
        ParentSystemId,
        RelationshipRank
    )
    VALUES
    (
        NEWID(),
        @RootSystemId,
        NULL,
        1
    );
END;
GO

SELECT
    TASK27_ROOT_SYSTEM_FOUND =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.pb_systems
            WHERE SystemId = '826d77cf-8b1a-a301-4efe-1113e5a17e88'
        ) THEN 1 ELSE 0 END,
    TASK27_ROOT_RELATIONSHIP_EXISTS =
        CASE WHEN EXISTS
        (
            SELECT 1
            FROM dbo.pb_system_relationships
            WHERE SystemId = '826d77cf-8b1a-a301-4efe-1113e5a17e88'
              AND ParentSystemId IS NULL
              AND RelationshipRank = 1
        ) THEN 1 ELSE 0 END,
    TASK27_ROOT_ROW_COUNT =
        (
            SELECT COUNT(*)
            FROM dbo.pb_system_relationships
            WHERE ParentSystemId IS NULL
        );