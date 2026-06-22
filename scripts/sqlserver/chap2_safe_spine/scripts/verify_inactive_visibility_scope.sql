-- Task 21 verification: inactive PB Visibility Scope
-- 2026-06-22 12:04 AM PT

DECLARE @SystemId UNIQUEIDENTIFIER = '826d77cf-8b1a-a301-4efe-1113e5a17e88';
DECLARE @PbVisibilitySchemaExists INT = 0;
DECLARE @BaselineScopesExist INT = 0;
DECLARE @RuntimeVisibilityColumnCount INT = 0;
DECLARE @RuntimeVisibilityFkCount INT = 0;
DECLARE @SpBucketsSeparate INT = 0;
DECLARE @BaselineScopeCount INT = 0;
DECLARE @SpBucketCouplingCount INT = 0;

IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NOT NULL
BEGIN
    SET @PbVisibilitySchemaExists = 1;
END;

SELECT @BaselineScopeCount = COUNT(*)
FROM dbo.pb_visibility_scopes
WHERE SystemId = @SystemId
  AND ScopeName IN (N'Whole System', N'Owners', N'Private');

IF @BaselineScopeCount = 3
BEGIN
    SET @BaselineScopesExist = 1;
END;

SELECT @RuntimeVisibilityColumnCount = COUNT(*)
FROM sys.columns AS c
WHERE c.object_id IN
(
    OBJECT_ID(N'dbo.pb_members'),
    OBJECT_ID(N'dbo.pb_front_history'),
    OBJECT_ID(N'dbo.pb_custom_fields'),
    OBJECT_ID(N'dbo.pb_privacy_buckets')
)
AND c.name IN
(
    N'VisibilityScopeId',
    N'VisibilityScopeName',
    N'PbVisibilityScopeId',
    N'PbVisibilityScopeName'
);

SELECT @RuntimeVisibilityFkCount = COUNT(*)
FROM sys.foreign_keys AS fk
WHERE
    fk.referenced_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes')
    AND fk.parent_object_id IN
    (
        OBJECT_ID(N'dbo.pb_members'),
        OBJECT_ID(N'dbo.pb_front_history'),
        OBJECT_ID(N'dbo.pb_custom_fields'),
        OBJECT_ID(N'dbo.pb_privacy_buckets')
    );

SELECT @SpBucketCouplingCount = COUNT(*)
FROM sys.foreign_keys AS fk
WHERE
    (fk.parent_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets') AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes'))
    OR
    (fk.parent_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes') AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets'));

IF OBJECT_ID(N'dbo.pb_privacy_buckets', N'U') IS NOT NULL
   AND @SpBucketCouplingCount = 0
BEGIN
    SET @SpBucketsSeparate = 1;
END;

PRINT 'TASK21_PB_VISIBILITY_SCHEMA_EXISTS=' + CONVERT(NVARCHAR(10), @PbVisibilitySchemaExists);
PRINT 'TASK21_BASELINE_SCOPES_EXIST=' + CONVERT(NVARCHAR(10), @BaselineScopesExist);
PRINT 'TASK21_RUNTIME_TABLE_VISIBILITY_COLUMN_COUNT=' + CONVERT(NVARCHAR(10), @RuntimeVisibilityColumnCount);
PRINT 'TASK21_RUNTIME_TABLE_VISIBILITY_FK_COUNT=' + CONVERT(NVARCHAR(10), @RuntimeVisibilityFkCount);
PRINT 'TASK21_SP_BUCKETS_SEPARATE=' + CONVERT(NVARCHAR(10), @SpBucketsSeparate);

IF @PbVisibilitySchemaExists = 1
   AND @BaselineScopesExist = 1
   AND @RuntimeVisibilityColumnCount = 0
   AND @RuntimeVisibilityFkCount = 0
   AND @SpBucketsSeparate = 1
BEGIN
    PRINT 'TASK21_OK';
END
ELSE
BEGIN
    PRINT 'TASK21_REVIEW';
END;
