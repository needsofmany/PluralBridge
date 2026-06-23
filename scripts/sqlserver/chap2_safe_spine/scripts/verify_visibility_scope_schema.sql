DECLARE @SystemId UNIQUEIDENTIFIER =
    '826d77cf-8b1a-a301-4efe-1113e5a17e88';

DECLARE @VisibilityScopeTableExists INT = 0;
DECLARE @VisibilityScopeMemberTableExists INT = 0;
DECLARE @BaselineScopeCount INT = 0;
DECLARE @PrivacyBucketCount INT = 0;

IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NOT NULL
BEGIN
    SET @VisibilityScopeTableExists = 1;
END;

IF OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NOT NULL
BEGIN
    SET @VisibilityScopeMemberTableExists = 1;
END;

SELECT @BaselineScopeCount = COUNT(*)
FROM dbo.pb_visibility_scopes
WHERE SystemId = @SystemId
  AND ScopeName IN
  (
      N'Whole System',
      N'Owners',
      N'Private'
  );

SELECT @PrivacyBucketCount = COUNT(*)
FROM dbo.pb_privacy_buckets;

PRINT 'TASK18_6_VISIBILITY_SCOPE_TABLE_EXISTS=' + CONVERT(NVARCHAR(10), @VisibilityScopeTableExists);
PRINT 'TASK18_6_VISIBILITY_SCOPE_MEMBER_TABLE_EXISTS=' + CONVERT(NVARCHAR(10), @VisibilityScopeMemberTableExists);
PRINT 'TASK18_6_BASELINE_SCOPE_COUNT=' + CONVERT(NVARCHAR(10), @BaselineScopeCount);
PRINT 'TASK18_6_PRIVACY_BUCKET_COUNT=' + CONVERT(NVARCHAR(10), @PrivacyBucketCount);

IF @VisibilityScopeTableExists = 1
   AND @VisibilityScopeMemberTableExists = 1
   AND @BaselineScopeCount = 3
   AND @PrivacyBucketCount = 2
BEGIN
    PRINT 'TASK18_6_OK';
END
ELSE
BEGIN
    PRINT 'TASK18_6_REVIEW';
END;