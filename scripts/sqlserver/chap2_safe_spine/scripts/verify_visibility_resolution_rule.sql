-- Task 20 verification: visibility resolution rule
-- 2026-06-21 11:55 PM PT

DECLARE @PbVisibilityScopesExist INT = 0;
DECLARE @PbVisibilityScopeMembersExist INT = 0;
DECLARE @SpPrivacyBucketsExist INT = 0;
DECLARE @DirectPbSpFkCount INT = 0;
DECLARE @SpBucketPbVisibilityColumnCount INT = 0;
DECLARE @RuleIsDesignOnly INT = 0;

IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NOT NULL SET @PbVisibilityScopesExist = 1;
IF OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NOT NULL SET @PbVisibilityScopeMembersExist = 1;
IF OBJECT_ID(N'dbo.pb_privacy_buckets', N'U') IS NOT NULL SET @SpPrivacyBucketsExist = 1;

SELECT @DirectPbSpFkCount = COUNT(*)
FROM sys.foreign_keys AS fk
WHERE
    (fk.parent_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets') AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes'))
    OR
    (fk.parent_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes') AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets'));

SELECT @SpBucketPbVisibilityColumnCount = COUNT(*)
FROM sys.columns
WHERE object_id = OBJECT_ID(N'dbo.pb_privacy_buckets')
  AND name IN (N'VisibilityScopeId', N'VisibilityScopeName', N'PbVisibilityScopeId', N'PbVisibilityScopeName');

IF @DirectPbSpFkCount = 0 AND @SpBucketPbVisibilityColumnCount = 0 SET @RuleIsDesignOnly = 1;

PRINT 'TASK20_PB_VISIBILITY_TABLES_EXIST=' + CONVERT(NVARCHAR(10), CASE WHEN @PbVisibilityScopesExist = 1 AND @PbVisibilityScopeMembersExist = 1 THEN 1 ELSE 0 END);
PRINT 'TASK20_SP_PRIVACY_BUCKETS_EXIST=' + CONVERT(NVARCHAR(10), @SpPrivacyBucketsExist);
PRINT 'TASK20_NO_DIRECT_PB_SP_COUPLING=' + CONVERT(NVARCHAR(10), CASE WHEN @DirectPbSpFkCount = 0 THEN 1 ELSE 0 END);
PRINT 'TASK20_RULE_IS_DESIGN_ONLY=' + CONVERT(NVARCHAR(10), @RuleIsDesignOnly);

IF @PbVisibilityScopesExist = 1 AND @PbVisibilityScopeMembersExist = 1 AND @SpPrivacyBucketsExist = 1 AND @DirectPbSpFkCount = 0 AND @RuleIsDesignOnly = 1
BEGIN
    PRINT 'TASK20_OK';
END
ELSE
BEGIN
    PRINT 'TASK20_REVIEW';
END;
