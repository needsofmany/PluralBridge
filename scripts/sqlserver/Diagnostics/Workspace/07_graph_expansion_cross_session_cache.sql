-- Read cached-plan metadata for dbo.pb_show_tree_step5_graph_expansion.
-- This script has not been executed.
-- It does not clear the plan cache, recompile a procedure, alter an object,
-- or execute graph expansion.
--
-- Run 06_graph_expansion_edges_after.sql once in one query window and once
-- in a second query window. Then run this script in either window.
-- A shared plan has the same plan_handle in both sessions and
-- optional_spid = 0. set_options can still differ between SSMS and the
-- application. Those are two plans, not session-specific plans.
-- Compilation events accompanied the measured first-call delay. The exact
-- waits in that delay were not identified. This script does not identify
-- them.
-- This login needs VIEW DATABASE STATE. An empty grid means no cached plan
-- was visible, not that the procedure is absent.

SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

IF OBJECT_ID(N'dbo.pb_show_tree_step5_graph_expansion', N'P') IS NULL
    THROW 50000, 'Stop: dbo.pb_show_tree_step5_graph_expansion is missing.', 1;

SELECT
    @@SPID AS SessionId,
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
  AND ps.object_id = OBJECT_ID(N'dbo.pb_show_tree_step5_graph_expansion')
  AND session_key.attribute = N'optional_spid'
  AND options.attribute = N'set_options'
ORDER BY
    optional_spid,
    set_options,
    ps.plan_handle;
