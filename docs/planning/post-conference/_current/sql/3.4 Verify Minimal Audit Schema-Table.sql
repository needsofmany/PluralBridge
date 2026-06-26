USE [PluralBridgeChap2SafeSpine];
GO

SELECT
    TASK3_AUDIT_TABLE_EXISTS =
        CASE
            WHEN OBJECT_ID(N'dbo.pb_member_write_audit', N'U') IS NOT NULL
            THEN 1
            ELSE 0
        END,

    TASK3_AUDIT_EXPECTED_COLUMNS =
        CASE
            WHEN (
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = 'pb_member_write_audit'
                  AND COLUMN_NAME IN
                  (
                      'MemberWriteAuditId',
                      'SystemId',
                      'AccountId',
                      'SystemMembershipId',
                      'MemberId',
                      'Operation',
                      'RequestTraceId',
                      'CreatedAtUtc'
                  )
            ) = 8
            THEN 1
            ELSE 0
        END,

    TASK3_AUDIT_PK_EXISTS =
        CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM sys.key_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.pb_member_write_audit')
                  AND type = 'PK'
            )
            THEN 1
            ELSE 0
        END,

    TASK3_AUDIT_FK_COUNT =
        (
            SELECT COUNT(*)
            FROM sys.foreign_keys
            WHERE parent_object_id = OBJECT_ID(N'dbo.pb_member_write_audit')
        ),

    TASK3_AUDIT_OPERATION_CONSTRAINT_EXISTS =
        CASE
            WHEN EXISTS
            (
                SELECT 1
                FROM sys.check_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.pb_member_write_audit')
                  AND name = 'CK_pb_member_write_audit_operation'
            )
            THEN 1
            ELSE 0
        END,

    TASK3_AUDIT_NO_TRIGGERS =
        CASE
            WHEN (
                SELECT COUNT(*)
                FROM sys.triggers
                WHERE parent_id = OBJECT_ID(N'dbo.pb_member_write_audit')
            ) = 0
            THEN 1
            ELSE 0
        END;
GO