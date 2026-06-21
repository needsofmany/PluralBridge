DECLARE @VisibilityScopesExist INT = 0;
DECLARE @VisibilityScopeMembersExist INT = 0;
DECLARE @PrivacyBucketsToVisibilityScopesFkCount INT = 0;
DECLARE @VisibilityScopesToPrivacyBucketsFkCount INT = 0;

IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NOT NULL
BEGIN
    SET @VisibilityScopesExist = 1;
END;

IF OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NOT NULL
BEGIN
    SET @VisibilityScopeMembersExist = 1;
END;

SELECT @PrivacyBucketsToVisibilityScopesFkCount = COUNT(*)
FROM sys.foreign_keys AS fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes');

SELECT @VisibilityScopesToPrivacyBucketsFkCount = COUNT(*)
FROM sys.foreign_keys AS fk
WHERE fk.parent_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes')
  AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets');

PRINT 'TASK19_3_VISIBILITY_SCOPES_EXIST=' + CONVERT(NVARCHAR(10), @VisibilityScopesExist);
PRINT 'TASK19_3_VISIBILITY_SCOPE_MEMBERS_EXIST=' + CONVERT(NVARCHAR(10), @VisibilityScopeMembersExist);
PRINT 'TASK19_3_SP_TO_PB_VISIBILITY_FK_COUNT=' + CONVERT(NVARCHAR(10), @PrivacyBucketsToVisibilityScopesFkCount);
PRINT 'TASK19_3_PB_VISIBILITY_TO_SP_FK_COUNT=' + CONVERT(NVARCHAR(10), @VisibilityScopesToPrivacyBucketsFkCount);

IF @VisibilityScopesExist = 1
   AND @VisibilityScopeMembersExist = 1
   AND @PrivacyBucketsToVisibilityScopesFkCount = 0
   AND @VisibilityScopesToPrivacyBucketsFkCount = 0
BEGIN
    PRINT 'TASK19_3_OK';
END
ELSE
BEGIN
    PRINT 'TASK19_3_REVIEW';
END
