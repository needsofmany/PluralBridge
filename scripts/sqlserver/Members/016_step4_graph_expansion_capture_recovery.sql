-- Restore dbo.pb_show_tree_step4_members_by_email to the definition that calls
-- graph expansion with a bare EXEC.
-- This script has not been executed.
-- Run it before 014_graph_expansion_result_set_recovery.sql, while traffic is stopped.
-- The procedure text is the step-4 definition from before 016.
-- GO starts a new batch, so a THROW before GO does not stop ALTER PROCEDURE.
-- A failed guard turns NOEXEC on. Later batches are compiled and not executed.
-- SET NOEXEC OFF still runs and restores the session.

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    RAISERROR('Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 16, 1);
    SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'dbo.pb_show_tree_step4_members_by_email', N'P') IS NULL
BEGIN
    RAISERROR('Stop: dbo.pb_show_tree_step4_members_by_email is missing.', 16, 1);
    SET NOEXEC ON;
END
GO

/****** Object:  StoredProcedure [dbo].[pb_show_tree_step4_members_by_email]    Script Date: 10/26/2026 8:10:29 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[pb_show_tree_step4_members_by_email]
    @Email NVARCHAR(320)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AccountId UNIQUEIDENTIFIER;

    SELECT @AccountId = a.AccountId
    FROM dbo.pb_accounts a
    WHERE a.Email = @Email;

    IF @AccountId IS NULL
    BEGIN
        RAISERROR('Account not found for email.', 16, 1);
        RETURN;
    END;

    SELECT N'Context' AS Section, @Email AS Email, @AccountId AS AccountId;

    --------------------------------------------------------------------------------
    -- Cleanup temp objects for repeatable runs
    --------------------------------------------------------------------------------
    IF OBJECT_ID('tempdb..#TopologyEdges') IS NOT NULL DROP TABLE #TopologyEdges;
    CREATE TABLE #TopologyEdges
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ChildObjectType NVARCHAR(32) NOT NULL,
        ChildObjectId UNIQUEIDENTIFIER NOT NULL,
        EdgeRank INT NOT NULL,
        EdgeSource NVARCHAR(64) NOT NULL
    );
    IF OBJECT_ID('tempdb..#SystemEdgeLabels') IS NOT NULL DROP TABLE #SystemEdgeLabels;
    CREATE TABLE #SystemEdgeLabels
    (
        SystemId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        EdgeLabel NVARCHAR(MAX) NULL
    );

    --------------------------------------------------------------------------------
    -- Step 1+2: topology seed builder. Its one result set is consumed here
    -- so the render result sets keep their current order.
    --------------------------------------------------------------------------------
    DECLARE @SeedRows TABLE
    (
        RowKind nvarchar(8) NOT NULL,
        ParentObjectType nvarchar(32) NULL,
        ParentObjectId uniqueidentifier NULL,
        ChildObjectType nvarchar(32) NULL,
        ChildObjectId uniqueidentifier NULL,
        EdgeRank int NULL,
        EdgeSource nvarchar(64) NULL,
        SystemId uniqueidentifier NULL,
        EdgeLabel nvarchar(max) NULL
    );

    DELETE FROM @SeedRows;

    INSERT INTO @SeedRows
    (
        RowKind,
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource,
        SystemId,
        EdgeLabel
    )
    EXEC dbo.pb_show_tree_step5_topology_seed_builder
        @AccountId = @AccountId;

    INSERT INTO #TopologyEdges
    (
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    )
    SELECT
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource
    FROM @SeedRows
    WHERE RowKind = N'edge';

    INSERT INTO #SystemEdgeLabels
    (
        SystemId,
        EdgeLabel
    )
    SELECT
        SystemId,
        EdgeLabel
    FROM @SeedRows
    WHERE RowKind = N'label';

    IF OBJECT_ID('tempdb..#WalkTopology') IS NOT NULL DROP TABLE #WalkTopology;
    CREATE TABLE #WalkTopology
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ObjectType NVARCHAR(32) NOT NULL,
        ObjectId UNIQUEIDENTIFIER NOT NULL,
        Depth INT NOT NULL,
        PathKey NVARCHAR(MAX) NOT NULL
    );

    --------------------------------------------------------------------------------
    -- Step 3: topology walker (refactored)
    --------------------------------------------------------------------------------
    EXEC dbo.pb_show_tree_step5_topology_walker
        @AccountId = @AccountId;

    IF OBJECT_ID('tempdb..#GraphEdges') IS NOT NULL DROP TABLE #GraphEdges;
    CREATE TABLE #GraphEdges
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ChildObjectType NVARCHAR(32) NOT NULL,
        ChildObjectId UNIQUEIDENTIFIER NOT NULL,
        EdgeRank INT NOT NULL,
        EdgeSource NVARCHAR(64) NOT NULL
    );

    --------------------------------------------------------------------------------
    -- Step 4: graph expansion (refactored)
    --------------------------------------------------------------------------------
    EXEC dbo.pb_show_tree_step5_graph_expansion
        @AccountId = @AccountId;

    IF OBJECT_ID('tempdb..#WalkGraph') IS NOT NULL DROP TABLE #WalkGraph;
    CREATE TABLE #WalkGraph
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ObjectType NVARCHAR(32) NOT NULL,
        ObjectId UNIQUEIDENTIFIER NOT NULL,
        Depth INT NOT NULL,
        PathKey NVARCHAR(MAX) NOT NULL
    );

    IF OBJECT_ID('tempdb..#GraphNodes') IS NOT NULL DROP TABLE #GraphNodes;
    CREATE TABLE #GraphNodes
    (
        ObjectType NVARCHAR(32) NOT NULL,
        ObjectId UNIQUEIDENTIFIER NOT NULL,
        NodeLabel NVARCHAR(MAX) NOT NULL
    );

    --------------------------------------------------------------------------------
    -- Step 5: final graph walk + node labeler (refactored)
    --------------------------------------------------------------------------------
    EXEC dbo.pb_show_tree_step5_final_graph_walk_and_node_labeler
        @AccountId = @AccountId;

    --------------------------------------------------------------------------------
    -- Step 5: render layer (refactored)
    --------------------------------------------------------------------------------
    EXEC dbo.pb_show_tree_step5_render_layer;
END;

GO
SET NOEXEC OFF;
GO
