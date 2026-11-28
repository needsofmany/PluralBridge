-- Read the seed result set after it is captured into the caller tables.
-- This script has not been executed.
-- Run it only after 011_topology_seed_result_set.sql. Compare its two grids
-- with the grids saved from 02_seed_edge_label_baseline.sql. The columns,
-- grouping, and sort are the same, including DuplicateCount.
--
-- Set @AccountId to the same account used for the baseline. The empty guid
-- stops the script. This script does not print an email.
--
-- This script writes only the session temporary tables and table variable
-- it creates. It does not change permanent data, clear the plan cache, or
-- alter a procedure.

SET NOCOUNT ON;

IF @@TRANCOUNT <> 0
    THROW 50000, 'Stop: run this script outside a transaction.', 1;

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
    THROW 50000, 'Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 1;

DECLARE @AccountId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

IF @AccountId IS NULL OR @AccountId = '00000000-0000-0000-0000-000000000000'
    THROW 50000, 'Stop: set @AccountId to the account being compared.', 1;

DROP TABLE IF EXISTS #TopologyEdges;
DROP TABLE IF EXISTS #SystemEdgeLabels;

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

SELECT
    N'edge' AS RowKind,
    ParentObjectType,
    ParentObjectId,
    ChildObjectType,
    ChildObjectId,
    EdgeRank,
    EdgeSource,
    COUNT(*) AS DuplicateCount
FROM #TopologyEdges
GROUP BY
    ParentObjectType,
    ParentObjectId,
    ChildObjectType,
    ChildObjectId,
    EdgeRank,
    EdgeSource
ORDER BY
    ParentObjectType,
    ParentObjectId,
    ChildObjectType,
    ChildObjectId,
    EdgeRank,
    EdgeSource;

SELECT
    N'label' AS RowKind,
    SystemId,
    EdgeLabel,
    COUNT(*) AS DuplicateCount
FROM #SystemEdgeLabels
GROUP BY
    SystemId,
    EdgeLabel
ORDER BY
    SystemId,
    EdgeLabel;
