-- Read the current seed procedure's complete edge rows and label text.
-- This script has not been executed.
-- Run it only before 011_topology_seed_result_set.sql, while
-- dbo.pb_show_tree_step5_topology_seed_builder writes #TopologyEdges and
-- #SystemEdgeLabels and returns no result set.
-- After 011, this bare EXEC leaves those tables empty and returns the
-- new result set as a grid. Use 03_seed_edge_label_after_capture.sql then.
--
-- Set @AccountId below to the account being compared. The empty guid stops
-- the script. Do not leave it. This script does not print an email.
--
-- The two result grids are grouped. DuplicateCount is the number of
-- identical rows. Save both grids. The after-capture script returns the
-- same columns so the values and duplicate multiplicities can be compared.
--
-- This script writes only the session temporary tables it creates. It does
-- not change permanent data, clear the plan cache, or alter a procedure.

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

EXEC dbo.pb_show_tree_step5_topology_seed_builder
    @AccountId = @AccountId;

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
