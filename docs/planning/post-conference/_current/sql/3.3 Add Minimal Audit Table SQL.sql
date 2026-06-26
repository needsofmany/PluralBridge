USE [PluralBridgeChap2SafeSpine];
GO

IF OBJECT_ID(N'dbo.pb_member_write_audit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.pb_member_write_audit
    (
        MemberWriteAuditId UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT DF_pb_member_write_audit_MemberWriteAuditId DEFAULT NEWID(),

        SystemId UNIQUEIDENTIFIER NOT NULL,

        AccountId UNIQUEIDENTIFIER NOT NULL,

        SystemMembershipId UNIQUEIDENTIFIER NULL,

        MemberId UNIQUEIDENTIFIER NOT NULL,

        Operation NVARCHAR(32) NOT NULL,

        RequestTraceId NVARCHAR(100) NULL,

        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_pb_member_write_audit_CreatedAtUtc DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_pb_member_write_audit
            PRIMARY KEY (MemberWriteAuditId),

        CONSTRAINT FK_pb_member_write_audit_systems
            FOREIGN KEY (SystemId)
            REFERENCES dbo.pb_systems (SystemId),

        CONSTRAINT FK_pb_member_write_audit_accounts
            FOREIGN KEY (AccountId)
            REFERENCES dbo.pb_accounts (AccountId),

        CONSTRAINT FK_pb_member_write_audit_system_memberships
            FOREIGN KEY (SystemMembershipId)
            REFERENCES dbo.pb_system_memberships (SystemMembershipId),

        CONSTRAINT FK_pb_member_write_audit_members
            FOREIGN KEY (MemberId)
            REFERENCES dbo.pb_members (MemberId),

        CONSTRAINT CK_pb_member_write_audit_operation
            CHECK (Operation IN (N'member.add', N'member.edit'))
    );

    CREATE INDEX IX_pb_member_write_audit_SystemId_CreatedAtUtc
        ON dbo.pb_member_write_audit (SystemId, CreatedAtUtc);

    CREATE INDEX IX_pb_member_write_audit_MemberId_CreatedAtUtc
        ON dbo.pb_member_write_audit (MemberId, CreatedAtUtc);
END;
GO