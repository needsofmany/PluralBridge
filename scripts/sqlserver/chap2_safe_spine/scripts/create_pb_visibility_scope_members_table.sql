IF OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_visibility_scope_members
    (
        VisibilityScopeMemberId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_pb_visibility_scope_members
            PRIMARY KEY CLUSTERED,

        VisibilityScopeId UNIQUEIDENTIFIER NOT NULL,

        MemberId UNIQUEIDENTIFIER NOT NULL,

        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_pb_visibility_scope_members_CreatedAtUtc
            DEFAULT (SYSUTCDATETIME())
    );

    PRINT 'TASK18_3_CREATED';
END
ELSE
BEGIN
    PRINT 'TASK18_3_ALREADY_EXISTS';
END;

IF OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.pb_visibility_scope_members', N'VisibilityScopeMemberId') IS NOT NULL
   AND COL_LENGTH(N'dbo.pb_visibility_scope_members', N'VisibilityScopeId') IS NOT NULL
   AND COL_LENGTH(N'dbo.pb_visibility_scope_members', N'MemberId') IS NOT NULL
   AND COL_LENGTH(N'dbo.pb_visibility_scope_members', N'CreatedAtUtc') IS NOT NULL
BEGIN
    PRINT 'TASK18_3_OK';
END
ELSE
BEGIN
    PRINT 'TASK18_3_REVIEW';
END
