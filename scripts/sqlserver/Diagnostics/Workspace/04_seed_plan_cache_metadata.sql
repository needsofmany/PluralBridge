-- Read cached-plan metadata for dbo.pb_show_tree_step5_topology_seed_builder.
-- This script has not been executed.
-- It does not clear the plan cache, recompile a procedure, alter an object,
-- or add a hint. It selects metadata only.
--
-- optional_spid is the session key. A nonzero value means that cache entry
-- is tied to one session. set_options is the option bitmask for that entry.
-- execution_count is from sys.dm_exec_procedure_stats. usecounts is from
-- sys.dm_exec_cached_plans. plan_handle identifies the cache entry.
-- plan_generation_num greater than 1 means that entry has been recompiled.
-- Separate handles are separate cache entries.
--
-- This login needs VIEW DATABASE STATE. An empty grid means no cached plan
-- for this procedure was visible, not that the procedure is absent.
-- Run it before deployment for a baseline and again after deployment and
-- ordinary application traffic. Do not treat a later reading as proof that
-- the new plan is shared until the same account has been opened from more
-- than one session.

SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

IF OBJECT_ID(N'dbo.pb_show_tree_step5_topology_seed_builder', N'P') IS NULL
    THROW 50000, 'Stop: dbo.pb_show_tree_step5_topology_seed_builder is missing.', 1;

SELECT
    OBJECT_SCHEMA_NAME(ps.object_id) AS SchemaName,
    OBJECT_NAME(ps.object_id) AS ProcedureName,
    CONVERT(int, session_key.value) AS optional_spid,
    CONVERT(int, options.value) AS set_options,
    ps.execution_count,
    ps.plan_generation_num,
    cp.usecounts,
    ps.plan_handle
FROM sys.dm_exec_procedure_stats AS ps
INNER JOIN sys.dm_exec_cached_plans AS cp
    ON cp.plan_handle = ps.plan_handle
CROSS APPLY sys.dm_exec_plan_attributes(ps.plan_handle) AS session_key
CROSS APPLY sys.dm_exec_plan_attributes(ps.plan_handle) AS options
WHERE ps.database_id = DB_ID()
  AND ps.object_id = OBJECT_ID(N'dbo.pb_show_tree_step5_topology_seed_builder')
  AND session_key.attribute = N'optional_spid'
  AND options.attribute = N'set_options'
ORDER BY
    optional_spid,
    set_options,
    ps.plan_handle;
