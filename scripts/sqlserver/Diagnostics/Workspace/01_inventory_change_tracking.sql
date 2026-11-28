-- Read-only inventory for the workspace change-tracking contract.
-- Run in SSMS with the window database set to PluralBridgeChap2SafeSpine.
-- Use Results to Grid. Results to Text cuts values at 256 characters by default;
-- do not use it for the procedure-definition grids.
--
-- This script does not insert, update, delete, merge, truncate, or alter
-- any application table. It does not create a permanent object. The only
-- writes are table variables in this session.
-- It does not select credentials, connection strings, image bytes, emails,
-- or other row content from application tables.
--
-- A missing object is reported as missing. A null procedure definition means
-- the module is encrypted or this login lacks VIEW DEFINITION. Change-tracking
-- catalog views are skipped when this login lacks VIEW DATABASE STATE.
-- Those skips are result rows, not evidence that the feature is absent.

SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

DECLARE @Targets TABLE
(
    ObjectName sysname NOT NULL PRIMARY KEY,
    Concern nvarchar(80) NOT NULL
);

INSERT INTO @Targets (ObjectName, Concern)
VALUES
    (N'pb_account_object_placements', N'graph'),
    (N'pb_object_memberships', N'access'),
    (N'pb_object_membership_roles', N'write-authorization'),
    (N'pb_system_memberships', N'graph'),
    (N'pb_system_membership_statuses', N'access-lookup'),
    (N'pb_system_membership_roles', N'write-authorization-unused-by-current-read'),
    (N'pb_roles', N'write-authorization'),
    (N'pb_system_relationships', N'graph-and-labels'),
    (N'pb_groups', N'graph-and-labels'),
    (N'pb_group_members', N'graph'),
    (N'pb_place_members', N'graph'),
    (N'pb_places', N'labels'),
    (N'pb_systems', N'labels'),
    (N'pb_members', N'graph-member-and-labels'),
    (N'pb_custom_fields', N'labels'),
    (N'pb_front_history', N'fronting'),
    (N'pb_front_history_members', N'fronting'),
    (N'pb_member_avatars', N'avatar'),
    (N'pb_anchor_visibility_permissions', N'placement-setting'),
    (N'pb_member_visibility_permissions', N'placement-setting'),
    (N'pb_accounts', N'identity-lookup-only');

DECLARE @Procedures TABLE
(
    ProcedureName sysname NOT NULL PRIMARY KEY
);

INSERT INTO @Procedures (ProcedureName)
VALUES
    (N'pb_show_tree_step4_members_by_email'),
    (N'pb_show_tree_step5_topology_seed_builder'),
    (N'pb_show_tree_step5_topology_walker'),
    (N'pb_show_tree_step5_graph_expansion'),
    (N'pb_show_tree_step5_final_graph_walk_and_node_labeler'),
    (N'pb_show_tree_step5_render_layer'),
    (N'pb_get_anchor_member_profiles_by_email');

SELECT
    N'limitations' AS ResultSet,
    N'VIEW DEFINITION' AS CheckedPermission,
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION') AS HasPermission,
    CASE
        WHEN HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DEFINITION') = 1
            THEN N'Procedure and trigger definitions can be read, subject to object-level denial.'
        ELSE N'OBJECT_DEFINITION will be null. A null definition does not mean the procedure is absent.'
    END AS CompletenessNote

UNION ALL

SELECT
    N'limitations',
    N'VIEW DATABASE STATE',
    HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DATABASE STATE'),
    CASE
        WHEN HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DATABASE STATE') = 1
            THEN N'Change-tracking database and table catalog views will be queried.'
        ELSE N'Change-tracking catalog views will be skipped. Skip is not evidence they are disabled.'
    END;

SELECT
    N'isolation' AS ResultSet,
    d.name AS DatabaseName,
    d.is_read_committed_snapshot_on AS ReadCommittedSnapshot,
    d.snapshot_isolation_state AS SnapshotIsolationState,
    d.snapshot_isolation_state_desc AS SnapshotIsolationStateDesc,
    d.containment AS Containment,
    d.collation_name AS CollationName
FROM sys.databases AS d
WHERE d.database_id = DB_ID();

SELECT
    N'tables' AS ResultSet,
    t.ObjectName,
    t.Concern,
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
INNER JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.columns AS c
    ON c.object_id = tb.object_id
INNER JOIN sys.types AS ty
    ON ty.user_type_id = c.user_type_id
WHERE c.name NOT LIKE N'%Password%'
  AND c.name NOT LIKE N'%Secret%'
  AND c.name NOT LIKE N'%Credential%'
  AND c.name <> N'ImageBytes'
ORDER BY src.ObjectName, c.column_id;

SELECT
    N'disputed-objects' AS ResultSet,
    v.ObjectName,
    v.ColumnName,
    CASE WHEN tb.object_id IS NULL THEN N'missing-table' ELSE N'present-table' END AS TableState,
    CASE
        WHEN tb.object_id IS NULL THEN N'missing-column'
        WHEN c.column_id IS NULL THEN N'missing-column'
        ELSE N'present-column'
    END AS ColumnState,
    c.is_nullable AS IsNullable,
    ty.name AS TypeName
FROM
(
    VALUES
        (N'pb_object_membership_roles', N'ObjectMembershipId'),
        (N'pb_object_membership_roles', N'RoleId'),
        (N'pb_anchor_visibility_permissions', N'AccountId'),
        (N'pb_anchor_visibility_permissions', N'SystemId'),
        (N'pb_anchor_visibility_permissions', N'GroupId'),
        (N'pb_anchor_visibility_permissions', N'PlaceId'),
        (N'pb_member_visibility_permissions', N'MemberId'),
        (N'pb_member_visibility_permissions', N'GroupMemberId'),
        (N'pb_member_visibility_permissions', N'PlaceMemberId'),
        (N'pb_member_avatars', N'MemberId'),
        (N'pb_member_avatars', N'ContentType'),
        (N'pb_member_avatars', N'UpdatedAtUtc'),
        (N'pb_front_history', N'SystemId'),
        (N'pb_front_history', N'MemberId'),
        (N'pb_front_history', N'EndTimeMs')
) AS v (ObjectName, ColumnName)
LEFT JOIN sys.tables AS tb
    ON tb.name = v.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.columns AS c
    ON c.object_id = tb.object_id
   AND c.name = v.ColumnName
LEFT JOIN sys.types AS ty
    ON ty.user_type_id = c.user_type_id
ORDER BY v.ObjectName, v.ColumnName;

SELECT
    N'keys' AS ResultSet,
    src.ObjectName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_primary_key AS IsPrimaryKey,
    i.is_unique AS IsUnique,
    i.is_unique_constraint AS IsUniqueConstraint,
    STRING_AGG(col.name, N',') WITHIN GROUP (ORDER BY ic.key_ordinal, ic.index_column_id) AS KeyColumns
FROM @Targets AS src
INNER JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.indexes AS i
    ON i.object_id = tb.object_id
   AND i.type > 0
INNER JOIN sys.index_columns AS ic
    ON ic.object_id = i.object_id
   AND ic.index_id = i.index_id
   AND ic.is_included_column = 0
INNER JOIN sys.columns AS col
    ON col.object_id = ic.object_id
   AND col.column_id = ic.column_id
GROUP BY
    src.ObjectName,
    i.name,
    i.type_desc,
    i.is_primary_key,
    i.is_unique,
    i.is_unique_constraint,
    i.index_id
ORDER BY src.ObjectName, i.index_id;

SELECT
    N'foreign-keys' AS ResultSet,
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ParentTable,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    fk.delete_referential_action_desc AS OnDelete,
    fk.update_referential_action_desc AS OnUpdate,
    STRING_AGG(pc.name + N'->' + rc.name, N',') WITHIN GROUP (ORDER BY fkc.constraint_column_id) AS ColumnMap
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc
    ON fkc.constraint_object_id = fk.object_id
INNER JOIN sys.columns AS pc
    ON pc.object_id = fkc.parent_object_id
   AND pc.column_id = fkc.parent_column_id
INNER JOIN sys.columns AS rc
    ON rc.object_id = fkc.referenced_object_id
   AND rc.column_id = fkc.referenced_column_id
WHERE fk.parent_object_id IN
(
    SELECT tb.object_id
    FROM @Targets AS src
    INNER JOIN sys.tables AS tb
        ON tb.name = src.ObjectName
       AND tb.schema_id = SCHEMA_ID(N'dbo')
)
OR fk.referenced_object_id IN
(
    SELECT tb.object_id
    FROM @Targets AS src
    INNER JOIN sys.tables AS tb
        ON tb.name = src.ObjectName
       AND tb.schema_id = SCHEMA_ID(N'dbo')
)
GROUP BY
    fk.name,
    fk.parent_object_id,
    fk.referenced_object_id,
    fk.delete_referential_action_desc,
    fk.update_referential_action_desc
ORDER BY ParentTable, ForeignKeyName;

SELECT
    N'triggers' AS ResultSet,
    src.ObjectName AS ParentTable,
    tr.name AS TriggerName,
    tr.is_disabled AS IsDisabled,
    tr.is_instead_of_trigger AS IsInsteadOf,
    OBJECTPROPERTY(tr.object_id, N'ExecIsInsertTrigger') AS FiresOnInsert,
    OBJECTPROPERTY(tr.object_id, N'ExecIsUpdateTrigger') AS FiresOnUpdate,
    OBJECTPROPERTY(tr.object_id, N'ExecIsDeleteTrigger') AS FiresOnDelete,
    CASE
        WHEN OBJECTPROPERTY(tr.object_id, N'IsEncrypted') = 1 THEN N'encrypted'
        WHEN OBJECT_DEFINITION(tr.object_id) IS NULL THEN N'definition-null'
        ELSE N'definition-available'
    END AS DefinitionState,
    LEN(OBJECT_DEFINITION(tr.object_id)) AS DefinitionLength
FROM @Targets AS src
INNER JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.triggers AS tr
    ON tr.parent_id = tb.object_id
ORDER BY src.ObjectName, tr.name;

SELECT
    N'trigger-definitions' AS ResultSet,
    src.ObjectName AS ParentTable,
    tr.name AS TriggerName,
    CAST
    (
        N'<definition><![CDATA['
        + REPLACE(ISNULL(OBJECT_DEFINITION(tr.object_id), N''), N']]>', N']]]]><![CDATA[>')
        + N']]></definition>'
        AS xml
    ) AS DefinitionXml
FROM @Targets AS src
INNER JOIN sys.tables AS tb
    ON tb.name = src.ObjectName
   AND tb.schema_id = SCHEMA_ID(N'dbo')
INNER JOIN sys.triggers AS tr
    ON tr.parent_id = tb.object_id
WHERE OBJECTPROPERTY(tr.object_id, N'IsEncrypted') = 0
  AND OBJECT_DEFINITION(tr.object_id) IS NOT NULL
ORDER BY src.ObjectName, tr.name;

SELECT
    N'triggers-outside-inventory' AS ResultSet,
    COUNT(*) AS TriggerCount
FROM sys.triggers AS tr
INNER JOIN sys.tables AS tb
    ON tb.object_id = tr.parent_id
WHERE tb.schema_id = SCHEMA_ID(N'dbo')
  AND tb.name NOT IN (SELECT ObjectName FROM @Targets);

IF HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'VIEW DATABASE STATE') = 1
BEGIN
    SELECT
        N'change-tracking' AS ResultSet,
        CAST(N'database' AS nvarchar(32)) AS Scope,
        DB_NAME() AS ObjectName,
        CASE WHEN ct.database_id IS NULL THEN N'not-enabled' ELSE N'enabled' END AS TrackingState,
        ct.is_auto_cleanup_on AS AutoCleanup,
        ct.retention_period AS RetentionPeriod,
        ct.retention_period_units_desc AS RetentionUnits
    FROM (SELECT DB_ID() AS database_id) AS d
    LEFT JOIN sys.change_tracking_databases AS ct
        ON ct.database_id = d.database_id;

    SELECT
        N'change-tracking-tables' AS ResultSet,
        src.ObjectName,
        CASE
            WHEN tb.object_id IS NULL THEN N'missing-table'
            WHEN ctt.object_id IS NULL THEN N'not-enabled'
            ELSE N'enabled'
        END AS TrackingState,
        ctt.is_track_columns_updated_on AS TrackColumnsUpdated
    FROM @Targets AS src
    LEFT JOIN sys.tables AS tb
        ON tb.name = src.ObjectName
       AND tb.schema_id = SCHEMA_ID(N'dbo')
    LEFT JOIN sys.change_tracking_tables AS ctt
        ON ctt.object_id = tb.object_id
    ORDER BY src.ObjectName;
END
ELSE
BEGIN
    SELECT
        N'change-tracking' AS ResultSet,
        CAST(N'skipped' AS nvarchar(32)) AS Scope,
        DB_NAME() AS ObjectName,
        N'permission-denied' AS TrackingState,
        CAST(NULL AS bit) AS AutoCleanup,
        CAST(NULL AS int) AS RetentionPeriod,
        CAST(NULL AS nvarchar(20)) AS RetentionUnits;
END;

SELECT
    N'procedures' AS ResultSet,
    req.ProcedureName,
    CASE WHEN p.object_id IS NULL THEN N'missing' ELSE N'present' END AS ObjectState,
    p.create_date AS CreatedAt,
    p.modify_date AS ModifiedAt,
    CASE
        WHEN p.object_id IS NULL THEN N'missing'
        WHEN OBJECTPROPERTY(p.object_id, N'IsEncrypted') = 1 THEN N'encrypted'
        WHEN m.definition IS NULL THEN N'definition-null'
        ELSE N'definition-available'
    END AS DefinitionState,
    LEN(m.definition) AS DefinitionLength
FROM @Procedures AS req
LEFT JOIN sys.procedures AS p
    ON p.name = req.ProcedureName
   AND p.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.sql_modules AS m
    ON m.object_id = p.object_id
ORDER BY req.ProcedureName;

SELECT
    N'procedure-parameters' AS ResultSet,
    req.ProcedureName,
    CASE WHEN p.object_id IS NULL THEN N'missing-procedure' ELSE prm.name END AS ParameterName,
    prm.parameter_id AS ParameterId,
    ty.name AS TypeName,
    prm.max_length AS MaxLength,
    prm.is_output AS IsOutput,
    prm.has_default_value AS HasDefault,
    prm.default_value AS DefaultValue
FROM @Procedures AS req
LEFT JOIN sys.procedures AS p
    ON p.name = req.ProcedureName
   AND p.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.parameters AS prm
    ON prm.object_id = p.object_id
LEFT JOIN sys.types AS ty
    ON ty.user_type_id = prm.user_type_id
ORDER BY req.ProcedureName, prm.parameter_id;

SELECT
    N'procedure-definition-xml' AS ResultSet,
    req.ProcedureName,
    CAST
    (
        N'<definition><![CDATA['
        + REPLACE(ISNULL(m.definition, N''), N']]>', N']]]]><![CDATA[>')
        + N']]></definition>'
        AS xml
    ) AS DefinitionXml
FROM @Procedures AS req
LEFT JOIN sys.procedures AS p
    ON p.name = req.ProcedureName
   AND p.schema_id = SCHEMA_ID(N'dbo')
LEFT JOIN sys.sql_modules AS m
    ON m.object_id = p.object_id
ORDER BY req.ProcedureName;

;WITH Segments AS
(
    SELECT
        req.ProcedureName,
        m.definition AS DefinitionText
    FROM @Procedures AS req
    LEFT JOIN sys.procedures AS p
        ON p.name = req.ProcedureName
       AND p.schema_id = SCHEMA_ID(N'dbo')
    LEFT JOIN sys.sql_modules AS m
        ON m.object_id = p.object_id
),
Numbers AS
(
    SELECT TOP (400)
        ROW_NUMBER() OVER (ORDER BY a.object_id) - 1 AS SegmentIndex
    FROM sys.all_objects AS a
)
SELECT
    N'procedure-definition-segments' AS ResultSet,
    s.ProcedureName,
    n.SegmentIndex,
    SUBSTRING(s.DefinitionText, (n.SegmentIndex * 4000) + 1, 4000) AS DefinitionSegment
FROM Segments AS s
INNER JOIN Numbers AS n
    ON s.DefinitionText IS NOT NULL
   AND (n.SegmentIndex * 4000) < LEN(s.DefinitionText)
ORDER BY s.ProcedureName, n.SegmentIndex;
