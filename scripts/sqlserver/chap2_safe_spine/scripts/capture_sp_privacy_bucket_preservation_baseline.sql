DECLARE @PrivacyBucketsExist INT = 0;
DECLARE @PrivacyBucketCount INT = -1;
DECLARE @PbVisibilityColumnCount INT = 0;

IF OBJECT_ID(N'dbo.pb_privacy_buckets', N'U') IS NOT NULL
BEGIN
    SET @PrivacyBucketsExist = 1;

    SELECT @PrivacyBucketCount = COUNT(*)
    FROM dbo.pb_privacy_buckets;

    SELECT @PbVisibilityColumnCount = COUNT(*)
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.pb_privacy_buckets')
      AND name IN
      (
          N'VisibilityScopeId',
          N'VisibilityScopeName',
          N'PbVisibilityScopeId',
          N'PbVisibilityScopeName'
      );
END;

PRINT 'TASK19_2_PRIVACY_BUCKETS_EXIST=' + CONVERT(NVARCHAR(10), @PrivacyBucketsExist);
PRINT 'TASK19_2_PRIVACY_BUCKET_COUNT=' + CONVERT(NVARCHAR(10), @PrivacyBucketCount);
PRINT 'TASK19_2_PB_VISIBILITY_COLUMNS_ON_SP_BUCKETS=' + CONVERT(NVARCHAR(10), @PbVisibilityColumnCount);

IF @PrivacyBucketsExist = 1
   AND @PrivacyBucketCount = 2
   AND @PbVisibilityColumnCount = 0
BEGIN
    PRINT 'TASK19_2_OK';
END
ELSE
BEGIN
    PRINT 'TASK19_2_REVIEW';
END
