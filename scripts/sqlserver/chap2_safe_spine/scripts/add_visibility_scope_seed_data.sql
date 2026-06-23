DECLARE @SystemId UNIQUEIDENTIFIER =
    '826d77cf-8b1a-a301-4efe-1113e5a17e88';

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.pb_systems
    WHERE SystemId = @SystemId
)
BEGIN
    PRINT 'TASK18_5_REVIEW_SYSTEM_NOT_FOUND';
END
ELSE
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.pb_visibility_scopes
        WHERE SystemId = @SystemId
          AND ScopeName = N'Whole System'
    )
    BEGIN
        INSERT INTO dbo.pb_visibility_scopes
        (
            VisibilityScopeId,
            SystemId,
            ScopeName,
            ScopeDesc,
            IsSystemDefault,
            IsActive
        )
        VALUES
        (
            NEWID(),
            @SystemId,
            N'Whole System',
            N'Visible to the whole system.',
            1,
            1
        );

        PRINT 'TASK18_5_ADDED_WHOLE_SYSTEM';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_5_WHOLE_SYSTEM_ALREADY_EXISTS';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.pb_visibility_scopes
        WHERE SystemId = @SystemId
          AND ScopeName = N'Owners'
    )
    BEGIN
        INSERT INTO dbo.pb_visibility_scopes
        (
            VisibilityScopeId,
            SystemId,
            ScopeName,
            ScopeDesc,
            IsSystemDefault,
            IsActive
        )
        VALUES
        (
            NEWID(),
            @SystemId,
            N'Owners',
            N'Visible to system owners.',
            0,
            1
        );

        PRINT 'TASK18_5_ADDED_OWNERS';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_5_OWNERS_ALREADY_EXISTS';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.pb_visibility_scopes
        WHERE SystemId = @SystemId
          AND ScopeName = N'Private'
    )
    BEGIN
        INSERT INTO dbo.pb_visibility_scopes
        (
            VisibilityScopeId,
            SystemId,
            ScopeName,
            ScopeDesc,
            IsSystemDefault,
            IsActive
        )
        VALUES
        (
            NEWID(),
            @SystemId,
            N'Private',
            N'Visible only through an explicit private visibility rule.',
            0,
            1
        );

        PRINT 'TASK18_5_ADDED_PRIVATE';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_5_PRIVATE_ALREADY_EXISTS';
    END;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_visibility_scopes
    WHERE SystemId = @SystemId
      AND ScopeName = N'Whole System'
)
AND EXISTS
(
    SELECT 1
    FROM dbo.pb_visibility_scopes
    WHERE SystemId = @SystemId
      AND ScopeName = N'Owners'
)
AND EXISTS
(
    SELECT 1
    FROM dbo.pb_visibility_scopes
    WHERE SystemId = @SystemId
      AND ScopeName = N'Private'
)
BEGIN
    PRINT 'TASK18_5_OK';
END
ELSE
BEGIN
    PRINT 'TASK18_5_REVIEW';
END
