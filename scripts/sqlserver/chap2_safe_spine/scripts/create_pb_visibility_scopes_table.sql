IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_visibility_scopes
    (
        VisibilityScopeId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT PK_pb_visibility_scopes
            PRIMARY KEY CLUSTERED,

        SystemId UNIQUEIDENTIFIER NOT NULL,

        ScopeName NVARCHAR(100) NOT NULL,

        ScopeDesc NVARCHAR(500) NULL,

        IsSystemDefault BIT NOT NULL
            CONSTRAINT DF_pb_visibility_scopes_IsSystemDefault
            DEFAULT (0),

        IsActive BIT NOT NULL
            CONSTRAINT DF_pb_visibility_scopes_IsActive
            DEFAULT (1),

        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_pb_visibility_scopes_CreatedAtUtc
            DEFAULT (SYSUTCDATETIME()),

        UpdatedAtUtc DATETIME2(7) NULL
    );

    PRINT 'TASK18_2_CREATED';
END
ELSE
BEGIN
    PRINT 'TASK18_2_ALREADY_EXISTS';
END;

IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NOT NULL
BEGIN
    PRINT 'TASK18_2_OK';
END
ELSE
BEGIN
    PRINT 'TASK18_2_REVIEW';
END
