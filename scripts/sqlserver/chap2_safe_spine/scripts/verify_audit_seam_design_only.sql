-- Task 22 verification: audit seam design-only
-- 2026-06-22 12:13 AM PT

DECLARE @SpineTableCount INT = 0;
DECLARE @AuditTableExists INT = 0;
DECLARE @AuditFkCount INT = 0;
DECLARE @AuditTriggerCount INT = 0;
DECLARE @AuditSeamDesignOnly INT = 0;

SELECT @SpineTableCount = COUNT(*)
FROM sys.tables
WHERE name IN
(
    N'pb_accounts',
    N'pb_account_statuses',
    N'pb_roles',
    N'pb_system_memberships',
    N'pb_system_membership_statuses',
    N'pb_systems'
);

IF OBJECT_ID(N'dbo.pb_audit_events', N'U') IS NOT NULL
BEGIN
    SET @AuditTableExists = 1;
END;

SELECT @AuditFkCount = COUNT(*)
FROM sys.foreign_keys AS fk
WHERE fk.name LIKE N'%audit%'
   OR OBJECT_NAME(fk.parent_object_id) LIKE N'%audit%'
   OR OBJECT_NAME(fk.referenced_object_id) LIKE N'%audit%';

SELECT @AuditTriggerCount = COUNT(*)
FROM sys.triggers AS tr
WHERE tr.name LIKE N'%audit%';

IF @AuditTableExists = 0 AND @AuditFkCount = 0 AND @AuditTriggerCount = 0
BEGIN
    SET @AuditSeamDesignOnly = 1;
END;

PRINT 'TASK22_SPINE_TABLES_EXIST=' + CONVERT(NVARCHAR(10), CASE WHEN @SpineTableCount = 6 THEN 1 ELSE 0 END);
PRINT 'TASK22_AUDIT_TABLE_EXISTS=' + CONVERT(NVARCHAR(10), @AuditTableExists);
PRINT 'TASK22_AUDIT_FK_COUNT=' + CONVERT(NVARCHAR(10), @AuditFkCount);
PRINT 'TASK22_AUDIT_TRIGGER_COUNT=' + CONVERT(NVARCHAR(10), @AuditTriggerCount);
PRINT 'TASK22_AUDIT_SEAM_DESIGN_ONLY=' + CONVERT(NVARCHAR(10), @AuditSeamDesignOnly);

IF @SpineTableCount = 6
   AND @AuditTableExists = 0
   AND @AuditFkCount = 0
   AND @AuditTriggerCount = 0
   AND @AuditSeamDesignOnly = 1
BEGIN
    PRINT 'TASK22_OK';
END
ELSE
BEGIN
    PRINT 'TASK22_REVIEW';
END;
