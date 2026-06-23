SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_system_membership_statuses', N'U') IS NOT NULL
BEGIN
    THROW 50001, 'Stop: dbo.pb_system_membership_statuses already exists.', 1;
END;
GO

CREATE TABLE dbo.pb_system_membership_statuses
(
    MembershipStatusId int NOT NULL,
    StatusName nvarchar(64) NOT NULL,
    StatusDesc nvarchar(500) NOT NULL,
    DisplayOrder int NOT NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_pb_system_membership_statuses_IsActive DEFAULT (1),

    CONSTRAINT PK_pb_system_membership_statuses
        PRIMARY KEY CLUSTERED (MembershipStatusId),

    CONSTRAINT UQ_pb_system_membership_statuses_StatusName
        UNIQUE (StatusName),

    CONSTRAINT CK_pb_system_membership_statuses_DisplayOrder_Positive
        CHECK (DisplayOrder > 0)
);
GO

INSERT INTO dbo.pb_system_membership_statuses
(
    MembershipStatusId,
    StatusName,
    StatusDesc,
    DisplayOrder,
    IsActive
)
VALUES
(
    1,
    N'Active',
    N'Membership is active and currently grants system access.',
    10,
    1
),
(
    2,
    N'Invited',
    N'Membership invitation has been sent but has not been accepted yet.',
    20,
    1
),
(
    3,
    N'Suspended',
    N'Membership is temporarily disabled and does not currently grant access.',
    30,
    1
),
(
    4,
    N'Revoked',
    N'Membership has been revoked by an authorized account and no longer grants access.',
    40,
    1
),
(
    5,
    N'Left',
    N'Membership was ended by the member leaving the system.',
    50,
    1
);
GO

SELECT
    MembershipStatusId,
    StatusName,
    StatusDesc,
    DisplayOrder,
    IsActive
FROM dbo.pb_system_membership_statuses
ORDER BY DisplayOrder;
GO