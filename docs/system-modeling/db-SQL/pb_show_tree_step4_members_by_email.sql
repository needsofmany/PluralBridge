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
    IF OBJECT_ID('tempdb..#GraphEdges') IS NOT NULL DROP TABLE #GraphEdges;
    IF OBJECT_ID('tempdb..#WalkGraph') IS NOT NULL DROP TABLE #WalkGraph;
    IF OBJECT_ID('tempdb..#GraphNodes') IS NOT NULL DROP TABLE #GraphNodes;

    IF OBJECT_ID('tempdb..##LegacySystemRoots') IS NOT NULL DROP TABLE ##LegacySystemRoots;
    IF OBJECT_ID('tempdb..##LegacySystemHierarchy') IS NOT NULL DROP TABLE ##LegacySystemHierarchy;
    IF OBJECT_ID('tempdb..##TopologyEdges') IS NOT NULL DROP TABLE ##TopologyEdges;
    IF OBJECT_ID('tempdb..##WalkTopology') IS NOT NULL DROP TABLE ##WalkTopology;
    IF OBJECT_ID('tempdb..##ScopedSystems') IS NOT NULL DROP TABLE ##ScopedSystems;
    IF OBJECT_ID('tempdb..##GroupResolved') IS NOT NULL DROP TABLE ##GroupResolved;
    IF OBJECT_ID('tempdb..##GraphEdges') IS NOT NULL DROP TABLE ##GraphEdges;
    IF OBJECT_ID('tempdb..##WalkGraph') IS NOT NULL DROP TABLE ##WalkGraph;
    IF OBJECT_ID('tempdb..##GraphNodes') IS NOT NULL DROP TABLE ##GraphNodes;

    --------------------------------------------------------------------------------
    -- Step 1+2: topology seed builder (refactored)
    --------------------------------------------------------------------------------
    EXEC dbo.pb_show_tree_step5_topology_seed_builder
        @AccountId = @AccountId;

    --------------------------------------------------------------------------------
    -- Step 3: topology walker (refactored)
    --------------------------------------------------------------------------------
    EXEC dbo.pb_show_tree_step5_topology_walker
        @AccountId = @AccountId;

    --------------------------------------------------------------------------------
    -- Step 4: graph expansion (refactored)
    --------------------------------------------------------------------------------
    EXEC dbo.pb_show_tree_step5_graph_expansion
        @AccountId = @AccountId;

    IF OBJECT_ID('tempdb..#GraphEdges') IS NOT NULL DROP TABLE #GraphEdges;
    SELECT
        ge.ParentObjectType,
        ge.ParentObjectId,
        ge.ChildObjectType,
        ge.ChildObjectId,
        ge.EdgeRank,
        ge.EdgeSource
    INTO #GraphEdges
    FROM ##GraphEdges ge;

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


