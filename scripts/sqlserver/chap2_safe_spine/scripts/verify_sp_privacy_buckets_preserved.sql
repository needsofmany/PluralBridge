DECLARE @PrivacyBucketsExist INT = 0;
DECLARE @PrivacyBucketCount INT = -1;
DECLARE @PbVisibilityColumnCount INT = 0;
DECLARE @VisibilityScopesExist INT = 0;
DECLARE @VisibilityScopeMembersExist INT = 0;
DECLARE @PrivacyBucketsToVisibilityScopesFkCount INT = 0;
DECLARE @VisibilityScopesToPrivacyBucketsFkCount INT = 0;

IF OBJECT_ID(N'dbo.pb_privacy_buckets', N'U') IS NOT NULL
BEGIN
    SET @PrivacyBucketsExist = 1;
    SELECT @PrivacyBucketCount = COUNT(*) FROM dbo.pb_privacy_buckets;
    SELECT @PbVisibilityColumnCount = COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.pb_privacy_buckets') AND name IN (N'VisibilityScopeId', N'VisibilityScopeName', N'PbVisibilityScopeId', N'PbVisibilityScopeName');
END;

IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NOT NULL
BEGIN
    SET @VisibilityScopesExist = 1;
END;

IF OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NOT NULL
BEGIN
    SET @VisibilityScopeMembersExist = 1;
END;

SELECT @PrivacyBucketsToVisibilityScopesFkCount = COUNT(*) FROM sys.foreign_keys AS fk WHERE fk.parent_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets') AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes');
SELECT @VisibilityScopesToPrivacyBucketsFkCount = COUNT(*) FROM sys.foreign_keys AS fk WHERE fk.parent_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes') AND fk.referenced_object_id = OBJECT_ID(N'dbo.pb_privacy_buckets');

PRINT 'TASK19_PRIVACY_BUCKETS_EXIST=' + CONVERT(NVARCHAR(10), @PrivacyBucketsExist);
PRINT 'TASK19_PRIVACY_BUCKET_COUNT=' + CONVERT(NVARCHAR(10), @PrivacyBucketCount);
PRINT 'TASK19_PB_VISIBILITY_COLUMNS_ON_SP_BUCKETS=' + CONVERT(NVARCHAR(10), @PbVisibilityColumnCount);
PRINT 'TASK19_VISIBILITY_SCOPES_EXIST=' + CONVERT(NVARCHAR(10), @VisibilityScopesExist);
PRINT 'TASK19_VISIBILITY_SCOPE_MEMBERS_EXIST=' + CONVERT(NVARCHAR(10), @VisibilityScopeMembersExist);
PRINT 'TASK19_SP_TO_PB_VISIBILITY_FK_COUNT=' + CONVERT(NVARCHAR(10), @PrivacyBucketsToVisibilityScopesFkCount);
PRINT 'TASK19_PB_VISIBILITY_TO_SP_FK_COUNT=' + CONVERT(NVARCHAR(10), @VisibilityScopesToPrivacyBucketsFkCount);

IF @PrivacyBucketsExist = 1 AND @PrivacyBucketCount = 2 AND @PbVisibilityColumnCount = 0 AND @VisibilityScopesExist = 1 AND @VisibilityScopeMembersExist = 1 AND @PrivacyBucketsToVisibilityScopesFkCount = 0 AND @VisibilityScopesToPrivacyBucketsFkCount = 0
BEGIN
    PRINT 'TASK19_OK';
END
ELSE
BEGIN
    PRINT 'TASK19_REVIEW';
END;
