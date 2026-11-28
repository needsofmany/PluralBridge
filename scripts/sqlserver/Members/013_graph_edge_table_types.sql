-- Create the table types used by dbo.pb_show_tree_step5_graph_expansion.
-- This script has not been executed.
-- Run it before 014_graph_expansion_result_set.sql.
-- Creating these types does not change the current procedure.
-- Neither type has a primary key or a unique constraint.
-- An empty table variable of either type is a legitimate empty input.
-- Table-valued parameters cannot be omitted and cannot have a default.
-- Callers pass an empty table variable when they have no rows.
--
-- The calling login needs EXECUTE on the expansion procedure and
-- REFERENCES on dbo.pb_WalkTopologyRows and dbo.pb_GraphEdgeRows.
-- REFERENCES on schema dbo, or ownership of the database, also covers
-- the types. This script does not grant those permissions.
--
-- GO starts a new batch, so a THROW before GO does not stop CREATE TYPE.
-- A failed guard turns NOEXEC on. Later batches are compiled and not
-- executed. SET NOEXEC OFF still runs and restores the session.

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    RAISERROR('Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 16, 1);
    SET NOEXEC ON;
END
GO

IF TYPE_ID(N'dbo.pb_WalkTopologyRows') IS NOT NULL
   OR TYPE_ID(N'dbo.pb_GraphEdgeRows') IS NOT NULL
BEGIN
    RAISERROR('Stop: a graph expansion table type already exists.', 16, 1);
    SET NOEXEC ON;
END
GO

CREATE TYPE dbo.pb_WalkTopologyRows AS TABLE
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ObjectType nvarchar(32) NOT NULL,
    ObjectId uniqueidentifier NOT NULL,
    Depth int NOT NULL,
    PathKey nvarchar(max) NOT NULL
);
GO

CREATE TYPE dbo.pb_GraphEdgeRows AS TABLE
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ChildObjectType nvarchar(32) NOT NULL,
    ChildObjectId uniqueidentifier NOT NULL,
    EdgeRank int NOT NULL,
    EdgeSource nvarchar(64) NOT NULL
);
GO

SET NOEXEC OFF;
GO
