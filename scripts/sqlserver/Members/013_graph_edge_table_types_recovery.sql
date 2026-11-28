-- Drop the graph expansion table types.
-- This script has not been executed.
-- Run it only after 014_graph_expansion_result_set_recovery.sql, and only
-- after every caller has stopped passing these types.
-- Dropping a type that a procedure still references will fail.
-- This script does not change permissions.

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    RAISERROR('Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 16, 1);
    SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'dbo.pb_show_tree_step5_graph_expansion', N'P') IS NOT NULL
   AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.pb_show_tree_step5_graph_expansion')) LIKE N'%pb_WalkTopologyRows%'
BEGIN
    RAISERROR('Stop: restore the expansion procedure before dropping the table types.', 16, 1);
    SET NOEXEC ON;
END
GO

IF TYPE_ID(N'dbo.pb_GraphEdgeRows') IS NOT NULL
    DROP TYPE dbo.pb_GraphEdgeRows;
GO

IF TYPE_ID(N'dbo.pb_WalkTopologyRows') IS NOT NULL
    DROP TYPE dbo.pb_WalkTopologyRows;
GO

SET NOEXEC OFF;
GO
