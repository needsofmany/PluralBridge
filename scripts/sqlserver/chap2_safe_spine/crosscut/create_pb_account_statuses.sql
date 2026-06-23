SELECT DB_NAME() AS CurrentDatabase;
GO

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;
END;
GO

IF OBJECT_ID(N'dbo.pb_account_statuses', N'U') IS NOT NULL
BEGIN
    THROW 50001, 'Stop: dbo.pb_account_statuses already exists.', 1;
END;
GO

CREATE TABLE dbo.pb_account_statuses
(
    AccountStatusId int NOT NULL,
    StatusName nvarchar(64) NOT NULL,
    StatusDesc nvarchar(500) NOT NULL,
    DisplayOrder int NOT NULL,
    IsActive bit NOT NULL
        CONSTRAINT DF_pb_account_statuses_IsActive DEFAULT (1),

    CONSTRAINT PK_pb_account_statuses
        PRIMARY KEY CLUSTERED (AccountStatusId),

    CONSTRAINT UQ_pb_account_statuses_StatusName
        UNIQUE (StatusName),

    CONSTRAINT CK_pb_account_statuses_DisplayOrder_Positive
        CHECK (DisplayOrder > 0)
);
GO

INSERT INTO dbo.pb_account_statuses
(
    AccountStatusId,
    StatusName,
    StatusDesc,
    DisplayOrder,
    IsActive
)
VALUES
(
    1,
    N'Active',
    N'Account is active and may authenticate and use granted system memberships.',
    10,
    1
),
(
    2,
    N'PendingEmailVerification',
    N'Account has been created, but email verification has not been completed yet.',
    20,
    1
),
(
    3,
    N'Disabled',
    N'Account is administratively disabled and cannot authenticate or access systems.',
    30,
    1
),
(
    4,
    N'Locked',
    N'Account is temporarily locked due to security, risk, or administrative action.',
    40,
    1
),
(
    5,
    N'Deleted',
    N'Account has been marked deleted and is no longer available for authentication or membership access.',
    50,
    1
);
GO

SELECT
    AccountStatusId,
    StatusName,
    StatusDesc,
    DisplayOrder,
    IsActive
FROM dbo.pb_account_statuses
ORDER BY DisplayOrder;
GO