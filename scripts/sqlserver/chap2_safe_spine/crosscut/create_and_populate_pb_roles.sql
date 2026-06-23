CREATE TABLE dbo.pb_roles
(
    RoleId       int            NOT NULL,
    RoleName     nvarchar(64)   NOT NULL,
    RoleDesc     nvarchar(500)  NOT NULL,
    DisplayOrder int            NOT NULL,
    IsActive     bit            NOT NULL CONSTRAINT DF_pb_roles_IsActive DEFAULT (1),

    CONSTRAINT PK_pb_roles PRIMARY KEY (RoleId),
    CONSTRAINT UQ_pb_roles_RoleName UNIQUE (RoleName),
    CONSTRAINT CK_pb_roles_DisplayOrder_Positive CHECK (DisplayOrder > 0)
);
GO

INSERT INTO dbo.pb_roles
(
    RoleId,
    RoleName,
    RoleDesc,
    DisplayOrder,
    IsActive
)
VALUES
(
    1,
    N'Owner',
    N'Full control of the system, including membership management and system-level settings.',
    10,
    1
),
(
    2,
    N'Admin',
    N'Can manage system data and most settings, but does not outrank the owner.',
    20,
    1
),
(
    3,
    N'Editor',
    N'Can add and edit day-to-day system data such as members, fronting records, group notes, and avatars where allowed.',
    30,
    1
),
(
    4,
    N'Viewer',
    N'Can read system data allowed by privacy rules, with no edit rights.',
    40,
    1
),
(
    5,
    N'LimitedViewer',
    N'Can read only explicitly shared or privacy-filtered data.',
    50,
    1
);
GO

SELECT
    RoleId,
    RoleName,
    RoleDesc,
    DisplayOrder,
    IsActive
FROM dbo.pb_roles
ORDER BY DisplayOrder;
GO
