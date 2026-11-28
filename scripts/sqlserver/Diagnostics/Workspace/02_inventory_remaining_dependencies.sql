-- Read-only follow-up inventory for the two dependencies missing from
-- 01_inventory_change_tracking.sql: pb_anchor_relationships and
-- pb_visibility_types.
--
-- Run in SSMS with the window database set to PluralBridgeChap2SafeSpine.
-- Use Results to Grid.
--
-- This script does not insert, update, delete, merge, truncate, or alter
-- any application table. It does not create a permanent object. The only
-- writes are table variables in this session. It does not read application
-- rows, credentials, image bytes, or user data.
--
-- A missing table is reported as missing. Trigger text is returned as
-- numbered 4000-character segments. The completeness result compares the
-- sum of those segment lengths with LEN(OBJECT_DEFINITION). A null
-- definition means the trigger is encrypted or this login lacks
-- VIEW DEFINITION on it. That is not an empty trigger.

SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

DECLARE @Targets TABLE
(
    ObjectName sysname NOT NULL PRIMARY KEY
);

INSERT INTO @Targets (ObjectName)
VALUES
    (N'pb_anchor_relationships'),
    (N'pb_visibility_types');

SELECT
    N'limitations' AS ResultSet,
    N'VIEW DEFINITION' AS CheckedPermission,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION') AS HasPermission,
    CASE
        WHEN HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION') = 1
            THEN N'Trigger and constraint definitions can be read, subject to object-level denial.'
        ELSE N'Definitions will be null. A null definition does not mean the object is absent.'
    END AS CompletenessNote;

SELECT
    N'tables' AS ResultSet,
    t.ObjectName,
    CASE WHEN s.object_id IS NULL THEN N'missing' ELSE N'present' END AS ObjectState,
    s.create_date AS CreatedAt,
    s.modify_date AS ModifiedAt
FROM @Targets AS t
LEFT JOIN sys.tables AS s
    ON s.name = t.ObjectName
   AND s.schema_id = SCHEMA_ID(N'dbo')
ORDER BY t.ObjectName;

SELECT
    N'columns' AS ResultSet,
    src.ObjectName,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE N'present-table' END AS TableState,
    c.column_id AS ColumnId,
    c.name AS ColumnName,
    ty.name AS TypeName,
    c.max_length AS MaxLength,
    c.precision AS PrecisionValue,
    c.scale AS ScaleValue,
    c.is_nullable AS IsNullable,
    c.is_identity AS IsIdentity,
    c.is_computed AS IsComputed,
    CASE WHEN ty.name = N'timestamp' OR ty.system_type_id = 189 THEN 1 ELSE 0 END AS IsRowVersion
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.columns AS c
    ON c.object_id = tb.object_id
LEFT JOIN sys.types AS ty
    ON ty.user_type_id = c.user_type_id
ORDER BY src.ObjectName, c.column_id;

SELECT
    N'indexes' AS ResultSet,
    src.ObjectName,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE N'present-table' END AS TableState,
    i.name AS IndexName,
    i.index_id AS IndexId,
    i.type_desc AS IndexType,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique AS IsUnique,
    i.is_unique_constraint AS IsUniqueConstraint,
    i.has_filter AS HasFilter,
    i.filter_definition AS FilterDefinition,
    STRING_AGG(
        CASE WHEN ic.is_included_column = 0 THEN col.name END,
        N','
    ) WITHIN GROUP (ORDER BY ic.key_ordinal, ic.index_column_id) AS KeyColumns
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.indexes AS i
    ON i.object_id = tb.object_id
LEFT JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
   AND ic.is_included_column = 0
LEFT JOIN sys.columns AS col
    ON col.object_id = ic.object_id
   AND col.column_id = ic.column_id
GROUP BY
    src.ObjectName,
    tb.object_id,
    i.name,
    i.index_id,
    i.type_desc,
    i.is_primary_key,
    i.is_unique,
    i.is_unique_constraint,
    i.has_filter,
    i.filter_definition
ORDER BY src.ObjectName, i.index_id;

SELECT
    N'included-columns' AS ResultSet,
    src.ObjectName,
    i.name AS IndexName,
    col.name AS ColumnName,
    ic.index_column_id AS IndexColumnId
FROM @Targets AS src
INNER JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.indexes AS i
    ON i.object_id = tb.object_id
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
   AND ic.is_included_column = 1
INNER JOIN sys.columns AS col
    ON col.object_id = ic.object_id
   AND col.column_id = ic.column_id
ORDER BY src.ObjectName, i.name, ic.index_column_id;

SELECT
    N'included-columns-none' AS ResultSet,
    src.ObjectName,
    CASE
        WHEN tb.object_id IS NULL THEN N'missing-table'
        WHEN EXISTS
        (
            SELECT 1
            FROM sys.index_columns AS ic
            WHERE ic.object_id = tb.object_id
              AND ic.is_included_column = 1
        ) THEN N'has-included-columns'
        ELSE N'no-included-columns'
    END AS IncludedState
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
ORDER BY src.ObjectName;

SELECT
    N'foreign-keys' AS ResultSet,
    src.ObjectName,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE ISNULL(fk.name, N'no-foreign-key') END AS ForeignKeyName,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate,
    STRING_AGG(pc.name + N'->' + rc.name, N',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS ColumnMap
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.foreign_keys AS fk
    ON fk.parent_object_id = tb.object_id
LEFT JOIN sys.foreign_key_columns AS fkc
    ON fkc.constraint_object_id = fk.object_id
LEFT JOIN sys.columns AS pc
    ON pc.object_id = fkc.parent_object_id
   AND pc.column_id = fkc.parent_column_id
LEFT JOIN sys.columns AS rc
    ON rc.object_id = fkc.referenced_object_id
   AND rc.column_id = fkc.referenced_column_id
GROUP BY
    src.ObjectName,
    tb.object_id,
    fk.name,
    fk.referenced_object_id,
    fk.delete_referential_action_desc,
    fk.update_referential_action_desc
ORDER BY src.ObjectName, fk.name;

SELECT
    N'referenced-by' AS ResultSet,
    src.ObjectName AS ReferencedTable,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE ISNULL(fk.name, N'no-incoming-foreign-key') END AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.foreign_keys AS fk
    ON fk.referenced_object_id = tb.object_id
ORDER BY src.ObjectName, ParentTable, fk.name;

SELECT
    N'constraints' AS ResultSet,
    src.ObjectName,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE N'present-table' END AS TableState,
    cc.name AS ConstraintName,
    N'CHECK' AS ConstraintType,
    col.name AS ColumnName,
    cc.is_disabled AS IsDisabled,
    cc.is_not_trusted AS IsNotTrusted,
    LEN(cc.definition) AS DefinitionLength,
    cc.definition AS DefinitionText
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.check_constraints AS cc
    ON cc.parent_object_id = tb.object_id
LEFT JOIN sys.columns AS col
    ON col.object_id = cc.parent_object_id
   AND col.column_id = cc.parent_column_id

UNION ALL

SELECT
    N'constraints',
    src.ObjectName,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE N'present-table' END,
    dc.name,
    N'DEFAULT',
    col.name,
    CAST(0 AS bit),
    CAST(0 AS bit),
    LEN(dc.definition),
    dc.definition
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.default_constraints AS dc
    ON dc.parent_object_id = tb.object_id
LEFT JOIN sys.columns AS col
    ON col.object_id = dc.parent_object_id
   AND col.column_id = dc.parent_column_id
ORDER BY ObjectName, ConstraintType, ConstraintName;

SELECT
    N'triggers' AS ResultSet,
    src.ObjectName AS ParentTable,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE N'present-table' END AS TableState,
    tr.name AS TriggerName,
    CASE
        WHEN tb.object_id IS NULL THEN N'missing-table'
        WHEN tr.object_id IS NULL THEN N'no-trigger'
        WHEN OBJECTPROPERTY(tr.object_id, N'IsEncrypted') = 1 THEN N'encrypted'
        WHEN OBJECT_DEFINITION(tr.object_id) IS NULL THEN N'definition-null'
        ELSE N'definition-available'
    END AS DefinitionState,
    tr.is_disabled AS IsDisabled,
    tr.is_instead_of_trigger AS IsInsteadOf,
    OBJECTPROPERTY(tr.object_id, N'ExecIsInsertTrigger') AS FiresOnInsert,
    OBJECTPROPERTY(tr.object_id, N'ExecIsUpdateTrigger') AS FiresOnUpdate,
    OBJECTPROPERTY(tr.object_id, N'ExecIsDeleteTrigger') AS FiresOnDelete,
    LEN(OBJECT_DEFINITION(tr.object_id)) AS DefinitionLength
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.triggers AS tr
    ON tr.parent_id = tb.object_id
ORDER BY src.ObjectName, tr.name;

DECLARE @SegmentSize int = 4000;

;WITH TriggerText AS
(
    SELECT
        tb.name AS ParentTable,
        tr.name AS TriggerName,
        OBJECT_DEFINITION(tr.object_id) AS DefinitionText,
        LEN(OBJECT_DEFINITION(tr.object_id)) AS DefinitionLength
    FROM @Targets AS src
    INNER JOIN sys.tables AS tb
        ON tb.name = src.ObjectName
       AND tb.schema_id = SCHEMA_ID(N'dbo')
    INNER JOIN sys.triggers AS tr
        ON tr.parent_id = tb.object_id
    WHERE OBJECTPROPERTY(tr.object_id, N'IsEncrypted') = 0
      AND OBJECT_DEFINITION(tr.object_id) IS NOT NULL
),
Numbers AS
(
    SELECT TOP (400)
        ROW_NUMBER() OVER (ORDER BY a.object_id) - 1 AS SegmentIndex
    FROM sys.all_objects AS a
),
Segments AS
(
    SELECT
        t.ParentTable,
        t.TriggerName,
        t.DefinitionLength,
        n.SegmentIndex,
        SUBSTRING(t.DefinitionText, (n.SegmentIndex * @SegmentSize) + 1, @SegmentSize) AS DefinitionSegment
    FROM TriggerText AS t
    INNER JOIN Numbers AS n
        ON (n.SegmentIndex * @SegmentSize) < t.DefinitionLength
)
SELECT
    N'trigger-definition-segments' AS ResultSet,
    s.ParentTable,
    s.TriggerName,
    s.SegmentIndex,
    @SegmentSize AS SegmentSize,
    LEN(s.DefinitionSegment) AS SegmentLength,
    s.DefinitionSegment
FROM Segments AS s
ORDER BY s.ParentTable, s.TriggerName, s.SegmentIndex;

;WITH TriggerText AS
(
    SELECT
        tb.name AS ParentTable,
        tr.name AS TriggerName,
        OBJECT_DEFINITION(tr.object_id) AS DefinitionText,
        LEN(OBJECT_DEFINITION(tr.object_id)) AS DefinitionLength
    FROM @Targets AS src
    INNER JOIN sys.tables AS tb
        ON tb.name = src.ObjectName
       AND tb.schema_id = SCHEMA_ID(N'dbo')
    INNER JOIN sys.triggers AS tr
        ON tr.parent_id = tb.object_id
    WHERE OBJECTPROPERTY(tr.object_id, N'IsEncrypted') = 0
      AND OBJECT_DEFINITION(tr.object_id) IS NOT NULL
),
Numbers AS
(
    SELECT TOP (400)
        ROW_NUMBER() OVER (ORDER BY a.object_id) - 1 AS SegmentIndex
    FROM sys.all_objects AS a
),
Collected AS
(
    SELECT
        t.ParentTable,
        t.TriggerName,
        t.DefinitionLength,
        SUM(LEN(SUBSTRING(t.DefinitionText, (n.SegmentIndex * @SegmentSize) + 1, @SegmentSize))) AS CollectedLength,
        COUNT(*) AS SegmentCount
    FROM TriggerText AS t
    INNER JOIN Numbers AS n
        ON (n.SegmentIndex * @SegmentSize) < t.DefinitionLength
    GROUP BY t.ParentTable, t.TriggerName, t.DefinitionLength
)
SELECT
    N'trigger-definition-completeness' AS ResultSet,
    src.ObjectName AS ParentTable,
    CASE
        WHEN tb.object_id IS NULL THEN N'missing-table'
        WHEN tr.object_id IS NULL THEN N'no-trigger'
        WHEN OBJECTPROPERTY(tr.object_id, N'IsEncrypted') = 1 THEN N'encrypted'
        WHEN OBJECT_DEFINITION(tr.object_id) IS NULL THEN N'definition-null'
        WHEN c.CollectedLength = c.DefinitionLength THEN N'complete'
        ELSE N'truncated'
    END AS Completeness,
    tr.name AS TriggerName,
    c.DefinitionLength,
    c.CollectedLength,
    c.SegmentCount,
    @SegmentSize AS SegmentSize
FROM @Targets AS src
LEFT JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.triggers AS tr
    ON tr.parent_id = tb.object_id
LEFT JOIN Collected AS c
    ON c.ParentTable = src.ObjectName
   AND c.TriggerName = tr.name
ORDER BY src.ObjectName, tr.name;
