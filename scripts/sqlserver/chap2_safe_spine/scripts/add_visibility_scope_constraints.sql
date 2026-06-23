IF OBJECT_ID(N'dbo.pb_visibility_scopes', N'U') IS NULL
    OR OBJECT_ID(N'dbo.pb_visibility_scope_members', N'U') IS NULL
    OR OBJECT_ID(N'dbo.pb_systems', N'U') IS NULL
    OR OBJECT_ID(N'dbo.pb_members', N'U') IS NULL
BEGIN
    PRINT 'TASK18_4_REVIEW_MISSING_TABLE';
END
ELSE
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_pb_visibility_scopes_SystemId_ScopeName'
          AND parent_object_id = OBJECT_ID(N'dbo.pb_visibility_scopes')
    )
    BEGIN
        ALTER TABLE dbo.pb_visibility_scopes
        ADD CONSTRAINT UQ_pb_visibility_scopes_SystemId_ScopeName
        UNIQUE (SystemId, ScopeName);

        PRINT 'TASK18_4_ADDED_SCOPE_NAME_UNIQUE';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_4_SCOPE_NAME_UNIQUE_ALREADY_EXISTS';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE name = N'UQ_pb_visibility_scope_members_ScopeId_MemberId'
          AND parent_object_id = OBJECT_ID(N'dbo.pb_visibility_scope_members')
    )
    BEGIN
        ALTER TABLE dbo.pb_visibility_scope_members
        ADD CONSTRAINT UQ_pb_visibility_scope_members_ScopeId_MemberId
        UNIQUE (VisibilityScopeId, MemberId);

        PRINT 'TASK18_4_ADDED_SCOPE_MEMBER_UNIQUE';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_4_SCOPE_MEMBER_UNIQUE_ALREADY_EXISTS';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_pb_visibility_scopes_pb_systems'
    )
    BEGIN
        ALTER TABLE dbo.pb_visibility_scopes
        ADD CONSTRAINT FK_pb_visibility_scopes_pb_systems
        FOREIGN KEY (SystemId)
        REFERENCES dbo.pb_systems (SystemId);

        PRINT 'TASK18_4_ADDED_SCOPE_SYSTEM_FK';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_4_SCOPE_SYSTEM_FK_ALREADY_EXISTS';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_pb_visibility_scope_members_pb_visibility_scopes'
    )
    BEGIN
        ALTER TABLE dbo.pb_visibility_scope_members
        ADD CONSTRAINT FK_pb_visibility_scope_members_pb_visibility_scopes
        FOREIGN KEY (VisibilityScopeId)
        REFERENCES dbo.pb_visibility_scopes (VisibilityScopeId);

        PRINT 'TASK18_4_ADDED_SCOPE_MEMBER_SCOPE_FK';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_4_SCOPE_MEMBER_SCOPE_FK_ALREADY_EXISTS';
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE name = N'FK_pb_visibility_scope_members_pb_members'
    )
    BEGIN
        ALTER TABLE dbo.pb_visibility_scope_members
        ADD CONSTRAINT FK_pb_visibility_scope_members_pb_members
        FOREIGN KEY (MemberId)
        REFERENCES dbo.pb_members (MemberId);

        PRINT 'TASK18_4_ADDED_SCOPE_MEMBER_MEMBER_FK';
    END
    ELSE
    BEGIN
        PRINT 'TASK18_4_SCOPE_MEMBER_MEMBER_FK_ALREADY_EXISTS';
    END;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_pb_visibility_scopes_SystemId_ScopeName'
)
AND EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_pb_visibility_scope_members_ScopeId_MemberId'
)
AND EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_pb_visibility_scopes_pb_systems'
)
AND EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_pb_visibility_scope_members_pb_visibility_scopes'
)
AND EXISTS
(
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_pb_visibility_scope_members_pb_members'
)
BEGIN
    PRINT 'TASK18_4_OK';
END
ELSE
BEGIN
    PRINT 'TASK18_4_REVIEW';
END;