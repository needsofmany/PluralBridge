-- Compare graph edges after the expansion contract change.
-- This script has not been executed.
-- Run it only after 014_graph_expansion_result_set.sql and the caller
-- scripts that capture the result. Compare both grids with the grids
-- saved from 05_graph_expansion_edges_before.sql. The columns, grouping,
-- sort, and DuplicateCount are the same.
-- Set @DiagnosticEmail to the same account used for the before script.
-- The result grids do not repeat the email.
-- EmptyStart expands from an empty #GraphEdges. PreExisting inserts the
-- same diagnostic edge used by the before script, then expands again.
-- #GraphEdges is replaced only after the result is captured.
-- This script writes only session temporary tables and table variables.
-- It does not change permanent data, procedures, or cache settings.
-- Compilation events accompanied the measured first-call delay. The
-- exact waits in that delay were not identified. Do not treat a faster
-- call as a measurement of those waits.

SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

DECLARE @DiagnosticEmail nvarchar(320) = N'';

IF @DiagnosticEmail IS NULL OR @DiagnosticEmail = N''
    THROW 50000, 'Stop: set @DiagnosticEmail to the account email.', 1;

DECLARE @AccountId uniqueidentifier;
DECLARE @ResolvedAccount TABLE (AccountId uniqueidentifier NOT NULL);

INSERT INTO @ResolvedAccount (AccountId)
SELECT AccountId
FROM dbo.pb_accounts
WHERE Email = @DiagnosticEmail;

IF (SELECT COUNT(*) FROM @ResolvedAccount) <> 1
    THROW 50000, 'Stop: the diagnostic email must match exactly one account.', 1;

SELECT @AccountId = AccountId FROM @ResolvedAccount;

SELECT N'Account' AS Section, @AccountId AS AccountId;

DROP TABLE IF EXISTS #TopologyEdges;
DROP TABLE IF EXISTS #SystemEdgeLabels;
DROP TABLE IF EXISTS #WalkTopology;
DROP TABLE IF EXISTS #GraphEdges;

CREATE TABLE #TopologyEdges
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ChildObjectType nvarchar(32) NOT NULL,
    ChildObjectId uniqueidentifier NOT NULL,
    EdgeRank int NOT NULL,
    EdgeSource nvarchar(64) NOT NULL
);

CREATE TABLE #SystemEdgeLabels
(
    SystemId uniqueidentifier NOT NULL PRIMARY KEY,
    EdgeLabel nvarchar(max) NULL
);

CREATE TABLE #WalkTopology
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ObjectType nvarchar(32) NOT NULL,
    ObjectId uniqueidentifier NOT NULL,
    Depth int NOT NULL,
    PathKey nvarchar(max) NOT NULL
);

CREATE TABLE #GraphEdges
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ChildObjectType nvarchar(32) NOT NULL,
    ChildObjectId uniqueidentifier NOT NULL,
    EdgeRank int NOT NULL,
    EdgeSource nvarchar(64) NOT NULL
);

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

DECLARE @WalkTopology dbo.pb_WalkTopologyRows;
DECLARE @TopologyEdges dbo.pb_GraphEdgeRows;
DECLARE @ExistingGraphEdges dbo.pb_GraphEdgeRows;
DECLARE @ExpansionRows TABLE
(
    ParentObjectType nvarchar(32) NOT NULL,
    ParentObjectId uniqueidentifier NOT NULL,
    ChildObjectType nvarchar(32) NOT NULL,
    ChildObjectId uniqueidentifier NOT NULL,
    EdgeRank int NOT NULL,
    EdgeSource nvarchar(64) NOT NULL
);

INSERT INTO @SeedRows
(
    RowKind, ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId,
    EdgeRank, EdgeSource, SystemId, EdgeLabel
)
EXEC dbo.pb_show_tree_step5_topology_seed_builder
    @AccountId = @AccountId;

INSERT INTO #TopologyEdges
(
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
)
SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
FROM @SeedRows
WHERE RowKind = N'edge';

INSERT INTO #SystemEdgeLabels (SystemId, EdgeLabel)
SELECT SystemId, EdgeLabel
FROM @SeedRows
WHERE RowKind = N'label';

EXEC dbo.pb_show_tree_step5_topology_walker
    @AccountId = @AccountId;

DELETE FROM #GraphEdges;
DELETE FROM @WalkTopology;
DELETE FROM @TopologyEdges;
DELETE FROM @ExistingGraphEdges;
DELETE FROM @ExpansionRows;

INSERT INTO @WalkTopology (ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey)
SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
FROM #WalkTopology;

INSERT INTO @TopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
FROM #TopologyEdges;

INSERT INTO @ExistingGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
FROM #GraphEdges;

INSERT INTO @ExpansionRows
(
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
)
EXEC dbo.pb_show_tree_step5_graph_expansion
    @AccountId = @AccountId,
    @InputContractVersion = 1,
    @WalkTopology = @WalkTopology,
    @TopologyEdges = @TopologyEdges,
    @ExistingGraphEdges = @ExistingGraphEdges;

DELETE FROM #GraphEdges;

INSERT INTO #GraphEdges
(
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
)
SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
FROM @ExpansionRows;

SELECT
    N'EmptyStart' AS Section,
    ParentObjectType,
    ParentObjectId,
    ChildObjectType,
    ChildObjectId,
    EdgeRank,
    EdgeSource,
    COUNT(*) AS DuplicateCount
FROM #GraphEdges
GROUP BY
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
ORDER BY
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource;

DELETE FROM #GraphEdges;

INSERT INTO #GraphEdges
(
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
)
VALUES
(
    N'diagnostic',
    '11111111-1111-1111-1111-111111111111',
    N'diagnostic',
    '22222222-2222-2222-2222-222222222222',
    1,
    N'diagnostic'
);

DELETE FROM @WalkTopology;
DELETE FROM @TopologyEdges;
DELETE FROM @ExistingGraphEdges;
DELETE FROM @ExpansionRows;

INSERT INTO @WalkTopology (ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey)
SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
FROM #WalkTopology;

INSERT INTO @TopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
FROM #TopologyEdges;

INSERT INTO @ExistingGraphEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
FROM #GraphEdges;

INSERT INTO @ExpansionRows
(
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
)
EXEC dbo.pb_show_tree_step5_graph_expansion
    @AccountId = @AccountId,
    @InputContractVersion = 1,
    @WalkTopology = @WalkTopology,
    @TopologyEdges = @TopologyEdges,
    @ExistingGraphEdges = @ExistingGraphEdges;

DELETE FROM #GraphEdges;

INSERT INTO #GraphEdges
(
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
)
SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
FROM @ExpansionRows;

SELECT
    N'PreExisting' AS Section,
    ParentObjectType,
    ParentObjectId,
    ChildObjectType,
    ChildObjectId,
    EdgeRank,
    EdgeSource,
    COUNT(*) AS DuplicateCount
FROM #GraphEdges
GROUP BY
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
ORDER BY
    ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource;
