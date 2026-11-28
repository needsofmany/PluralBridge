-- Visibility and Permissions storage for review and installation.
-- This script has not been executed.
-- It does not change pb_members, including VisibilityTypeId, IsPrivate,
-- and PreventTrusted. It does not change pb_roles or pb_object_membership_roles.
-- Do not rerun scripts 001 through 003.
-- A member row is the selected member's setting on one existing membership:
-- pb_members.MemberId for a system, pb_group_members.GroupMemberId, or
-- pb_place_members.PlaceMemberId. An anchor row is that anchor object's
-- setting for one account. Inherit, unset, and explicit stay distinct.
-- This script does not choose a parent when an anchor has more than one.
-- Preflight checks and schema changes run in one transaction. A failed
-- check rolls that transaction back and makes no schema change.

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @ScriptTransaction bit = 0;

BEGIN TRY
    IF @@TRANCOUNT <> 0
        THROW 50000, 'Stop: this script must not run inside an existing transaction.', 1;

    BEGIN TRANSACTION;
    SET @ScriptTransaction = 1;

    IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
        THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

    IF OBJECT_ID(N'dbo.pb_anchor_visibility_permissions', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.pb_member_visibility_permissions', N'U') IS NOT NULL
        THROW 50000, 'Stop: visibility and permission settings tables already exist.', 1;

    CREATE TABLE dbo.pb_anchor_visibility_permissions
    (
        AnchorVisibilityPermissionId uniqueidentifier NOT NULL
            CONSTRAINT DF_pb_anchor_visibility_permissions_Id DEFAULT NEWSEQUENTIALID(),
        AccountId uniqueidentifier NOT NULL,
        SystemId uniqueidentifier NULL,
        GroupId uniqueidentifier NULL,
        PlaceId uniqueidentifier NULL,
        VisibilityMode nvarchar(16) NOT NULL,
        VisibilityTypeId int NULL,
        PermissionMode nvarchar(16) NOT NULL,
        PermissionCode nvarchar(16) NULL,
        UpdatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_pb_anchor_visibility_permissions_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_pb_anchor_visibility_permissions
            PRIMARY KEY CLUSTERED (AnchorVisibilityPermissionId),

        CONSTRAINT FK_pb_anchor_visibility_permissions_pb_accounts
            FOREIGN KEY (AccountId) REFERENCES dbo.pb_accounts (AccountId),

        CONSTRAINT FK_pb_anchor_visibility_permissions_pb_systems
            FOREIGN KEY (SystemId) REFERENCES dbo.pb_systems (SystemId),

        CONSTRAINT FK_pb_anchor_visibility_permissions_pb_groups
            FOREIGN KEY (GroupId) REFERENCES dbo.pb_groups (GroupId),

        CONSTRAINT FK_pb_anchor_visibility_permissions_pb_places
            FOREIGN KEY (PlaceId) REFERENCES dbo.pb_places (PlaceId),

        CONSTRAINT FK_pb_anchor_visibility_permissions_pb_visibility_types
            FOREIGN KEY (VisibilityTypeId) REFERENCES dbo.pb_visibility_types (VisibilityTypeId),

        CONSTRAINT CK_pb_anchor_visibility_permissions_Target
            CHECK (
                (SystemId IS NOT NULL AND GroupId IS NULL AND PlaceId IS NULL)
                OR (SystemId IS NULL AND GroupId IS NOT NULL AND PlaceId IS NULL)
                OR (SystemId IS NULL AND GroupId IS NULL AND PlaceId IS NOT NULL)
            ),

        CONSTRAINT CK_pb_anchor_visibility_permissions_Visibility
            CHECK (
                (
                    VisibilityMode = N'unset'
                    AND VisibilityTypeId IS NULL
                )
                OR (
                    VisibilityMode = N'inherit'
                    AND VisibilityTypeId IS NULL
                )
                OR (
                    VisibilityMode = N'explicit'
                    AND VisibilityTypeId IS NOT NULL
                    AND VisibilityTypeId IN (1, 2, 3)
                )
            ),

        CONSTRAINT CK_pb_anchor_visibility_permissions_Permission
            CHECK (
                (
                    PermissionMode = N'unset'
                    AND PermissionCode IS NULL
                )
                OR (
                    PermissionMode = N'inherit'
                    AND PermissionCode IS NULL
                )
                OR (
                    PermissionMode = N'explicit'
                    AND PermissionCode IS NOT NULL
                    AND PermissionCode IN (N'viewer', N'editor', N'manager', N'owner')
                )
            )
    );

    CREATE UNIQUE INDEX UQ_pb_anchor_visibility_permissions_Account_System
        ON dbo.pb_anchor_visibility_permissions (AccountId, SystemId)
        WHERE SystemId IS NOT NULL;

    CREATE UNIQUE INDEX UQ_pb_anchor_visibility_permissions_Account_Group
        ON dbo.pb_anchor_visibility_permissions (AccountId, GroupId)
        WHERE GroupId IS NOT NULL;

    CREATE UNIQUE INDEX UQ_pb_anchor_visibility_permissions_Account_Place
        ON dbo.pb_anchor_visibility_permissions (AccountId, PlaceId)
        WHERE PlaceId IS NOT NULL;

    CREATE TABLE dbo.pb_member_visibility_permissions
    (
        MemberVisibilityPermissionId uniqueidentifier NOT NULL
            CONSTRAINT DF_pb_member_visibility_permissions_Id DEFAULT NEWSEQUENTIALID(),
        MemberId uniqueidentifier NULL,
        GroupMemberId uniqueidentifier NULL,
        PlaceMemberId uniqueidentifier NULL,
        VisibilityMode nvarchar(16) NOT NULL,
        VisibilityTypeId int NULL,
        PermissionMode nvarchar(16) NOT NULL,
        PermissionCode nvarchar(16) NULL,
        UpdatedAtUtc datetime2(3) NOT NULL
            CONSTRAINT DF_pb_member_visibility_permissions_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_pb_member_visibility_permissions
            PRIMARY KEY CLUSTERED (MemberVisibilityPermissionId),

        CONSTRAINT FK_pb_member_visibility_permissions_pb_members
            FOREIGN KEY (MemberId) REFERENCES dbo.pb_members (MemberId),

        CONSTRAINT FK_pb_member_visibility_permissions_pb_group_members
            FOREIGN KEY (GroupMemberId) REFERENCES dbo.pb_group_members (GroupMemberId),

        CONSTRAINT FK_pb_member_visibility_permissions_pb_place_members
            FOREIGN KEY (PlaceMemberId) REFERENCES dbo.pb_place_members (PlaceMemberId),

        CONSTRAINT FK_pb_member_visibility_permissions_pb_visibility_types
            FOREIGN KEY (VisibilityTypeId) REFERENCES dbo.pb_visibility_types (VisibilityTypeId),

        CONSTRAINT CK_pb_member_visibility_permissions_Target
            CHECK (
                (MemberId IS NOT NULL AND GroupMemberId IS NULL AND PlaceMemberId IS NULL)
                OR (MemberId IS NULL AND GroupMemberId IS NOT NULL AND PlaceMemberId IS NULL)
                OR (MemberId IS NULL AND GroupMemberId IS NULL AND PlaceMemberId IS NOT NULL)
            ),

        CONSTRAINT CK_pb_member_visibility_permissions_Visibility
            CHECK (
                (
                    VisibilityMode = N'unset'
                    AND VisibilityTypeId IS NULL
                )
                OR (
                    VisibilityMode = N'inherit'
                    AND VisibilityTypeId IS NULL
                )
                OR (
                    VisibilityMode = N'explicit'
                    AND VisibilityTypeId IS NOT NULL
                    AND VisibilityTypeId IN (1, 2, 3)
                )
            ),

        CONSTRAINT CK_pb_member_visibility_permissions_Permission
            CHECK (
                (
                    PermissionMode = N'unset'
                    AND PermissionCode IS NULL
                )
                OR (
                    PermissionMode = N'inherit'
                    AND PermissionCode IS NULL
                )
                OR (
                    PermissionMode = N'explicit'
                    AND PermissionCode IS NOT NULL
                    AND PermissionCode IN (N'viewer', N'editor', N'manager', N'owner')
                )
            )
    );

    CREATE UNIQUE INDEX UQ_pb_member_visibility_permissions_Member
        ON dbo.pb_member_visibility_permissions (MemberId)
        WHERE MemberId IS NOT NULL;

    CREATE UNIQUE INDEX UQ_pb_member_visibility_permissions_GroupMember
        ON dbo.pb_member_visibility_permissions (GroupMemberId)
        WHERE GroupMemberId IS NOT NULL;

    CREATE UNIQUE INDEX UQ_pb_member_visibility_permissions_PlaceMember
        ON dbo.pb_member_visibility_permissions (PlaceMemberId)
        WHERE PlaceMemberId IS NOT NULL;

    COMMIT TRANSACTION;
    SET @ScriptTransaction = 0;
END TRY
BEGIN CATCH
    IF @ScriptTransaction = 1 AND @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT DB_NAME() AS CurrentDatabase;

SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS TypeName,
    c.max_length,
    c.is_nullable
FROM sys.tables AS t
INNER JOIN sys.columns AS c
    ON c.object_id = t.object_id
INNER JOIN sys.types AS ty
    ON ty.user_type_id = c.user_type_id
WHERE t.name IN (N'pb_anchor_visibility_permissions', N'pb_member_visibility_permissions')
ORDER BY t.name, c.column_id;

SELECT
    name AS ConstraintName,
    OBJECT_NAME(parent_object_id) AS TableName
FROM sys.check_constraints
WHERE OBJECT_NAME(parent_object_id) IN (N'pb_anchor_visibility_permissions', N'pb_member_visibility_permissions')
ORDER BY TableName, ConstraintName;

SELECT
    name AS IndexName,
    OBJECT_NAME(object_id) AS TableName,
    is_unique,
    has_filter
FROM sys.indexes
WHERE OBJECT_NAME(object_id) IN (N'pb_anchor_visibility_permissions', N'pb_member_visibility_permissions')
  AND name IS NOT NULL
ORDER BY TableName, IndexName;

SELECT
    COL_LENGTH(N'dbo.pb_members', N'VisibilityTypeId') AS MembersVisibilityTypeIdLength,
    COL_LENGTH(N'dbo.pb_members', N'IsPrivate') AS MembersIsPrivateLength,
    COL_LENGTH(N'dbo.pb_members', N'PreventTrusted') AS MembersPreventTrustedLength;

SELECT
    (SELECT COUNT(*) FROM dbo.pb_anchor_visibility_permissions) AS AnchorSettingRows,
    (SELECT COUNT(*) FROM dbo.pb_member_visibility_permissions) AS MemberSettingRows;
