/****** Object:  StoredProcedure [dbo].[pb_show_tree_step5_final_graph_walk_and_node_labeler]    Script Date: 10/31/2026 5:41:57 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


ALTER PROCEDURE [dbo].[pb_show_tree_step5_final_graph_walk_and_node_labeler]
    @AccountId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountId IS NULL
    BEGIN
        RAISERROR('@AccountId is required.', 16, 1);
        RETURN;
    END;

    IF OBJECT_ID('tempdb..#GraphEdges') IS NULL
    BEGIN
        RAISERROR('#GraphEdges must exist before calling final graph walk + node labeler.', 16, 1);
        RETURN;
    END;

    IF OBJECT_ID('tempdb..##WalkGraph') IS NOT NULL DROP TABLE ##WalkGraph;
    IF OBJECT_ID('tempdb..##GraphNodes') IS NOT NULL DROP TABLE ##GraphNodes;
    IF OBJECT_ID('tempdb..#SystemEdgeLabelsInput') IS NOT NULL DROP TABLE #SystemEdgeLabelsInput;

    CREATE TABLE #SystemEdgeLabelsInput
    (
        SystemId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        EdgeLabel NVARCHAR(MAX) NULL
    );

    IF OBJECT_ID('tempdb..#SystemEdgeLabels') IS NOT NULL
    BEGIN
        INSERT INTO #SystemEdgeLabelsInput (SystemId, EdgeLabel)
        SELECT
            sel.SystemId,
            sel.EdgeLabel
        FROM #SystemEdgeLabels sel;
    END
    ELSE IF OBJECT_ID('tempdb..##SystemEdgeLabels') IS NOT NULL
    BEGIN
        INSERT INTO #SystemEdgeLabelsInput (SystemId, EdgeLabel)
        SELECT
            sel.SystemId,
            sel.EdgeLabel
        FROM ##SystemEdgeLabels sel;
    END;

    ;WITH Walk AS
    (
        SELECT
            e.ParentObjectType,
            e.ParentObjectId,
            e.ChildObjectType AS ObjectType,
            e.ChildObjectId AS ObjectId,
            CAST(1 AS INT) AS Depth,
            CAST(
                RIGHT(
                    '00' + CAST(
                        CASE e.ChildObjectType
                            WHEN N'member' THEN 10
                            WHEN N'group'  THEN 20
                            WHEN N'system' THEN 30
                            WHEN N'place'  THEN 40
                            ELSE 99
                        END
                    AS VARCHAR(2)), 2
                )
                + N'|'
                + RIGHT('000000' + CAST(ISNULL(e.EdgeRank, 999999) AS VARCHAR(6)), 6)
                + N'|' + e.ChildObjectType
                + N'|' + CONVERT(NVARCHAR(36), e.ChildObjectId)
                AS NVARCHAR(MAX)
            ) AS PathKey,
            CAST(
                N'|' + e.ChildObjectType + N':' + CONVERT(NVARCHAR(36), e.ChildObjectId) + N'|'
                AS NVARCHAR(MAX)
            ) AS Visited
        FROM #GraphEdges e
        WHERE e.ParentObjectType = N'account'
          AND e.ParentObjectId = @AccountId

        UNION ALL

        SELECT
            e.ParentObjectType,
            e.ParentObjectId,
            e.ChildObjectType,
            e.ChildObjectId,
            w.Depth + 1,
            CAST(
                w.PathKey + N'.'
                + RIGHT(
                    '00' + CAST(
                        CASE e.ChildObjectType
                            WHEN N'member' THEN 10
                            WHEN N'group'  THEN 20
                            WHEN N'system' THEN 30
                            WHEN N'place'  THEN 40
                            ELSE 99
                        END
                    AS VARCHAR(2)), 2
                )
                + N'|'
                + RIGHT('000000' + CAST(ISNULL(e.EdgeRank, 999999) AS VARCHAR(6)), 6)
                + N'|' + e.ChildObjectType
                + N'|' + CONVERT(NVARCHAR(36), e.ChildObjectId)
                AS NVARCHAR(MAX)
            ),
            CAST(
                w.Visited + e.ChildObjectType + N':' + CONVERT(NVARCHAR(36), e.ChildObjectId) + N'|'
                AS NVARCHAR(MAX)
            )
        FROM #GraphEdges e
        JOIN Walk w
          ON e.ParentObjectType = w.ObjectType
         AND e.ParentObjectId = w.ObjectId
        WHERE w.Visited NOT LIKE N'%|' + e.ChildObjectType + N':' + CONVERT(NVARCHAR(36), e.ChildObjectId) + N'|%'
    )
    SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
    INTO ##WalkGraph
    FROM Walk
    OPTION (MAXRECURSION 200);

    ;WITH RawNodes AS
    (
        SELECT N'account' AS ObjectType, @AccountId AS ObjectId
        UNION
        SELECT ge.ParentObjectType, ge.ParentObjectId FROM #GraphEdges ge
        UNION
        SELECT ge.ChildObjectType, ge.ChildObjectId FROM #GraphEdges ge
    )
    SELECT
        rn.ObjectType,
        rn.ObjectId,
        NodeLabel =
            CASE
                WHEN rn.ObjectType = N'account' THEN N'Account'
                WHEN rn.ObjectType = N'system'  THEN
                    (
                        CASE
                            WHEN EXISTS
                            (
                                SELECT 1
                                FROM #GraphEdges p
                                WHERE p.ParentObjectType = N'system'
                                  AND p.ChildObjectType = N'system'
                                  AND p.ChildObjectId = rn.ObjectId
                            )
                            THEN N'Child System: '
                            ELSE N'System: '
                        END
                        + ISNULL(s.SystemName, N'(missing system)')
                        + CASE
                            WHEN sel.EdgeLabel IS NOT NULL AND LTRIM(RTRIM(sel.EdgeLabel)) <> N''
                                THEN N' (' + sel.EdgeLabel + N')'
                            ELSE N''
                          END
                    )
                WHEN rn.ObjectType = N'group'   THEN N'Group: '  + ISNULL(g.GroupName, N'(missing group)')
                WHEN rn.ObjectType = N'place'   THEN N'Place: '  + ISNULL(pl.PlaceName, N'(missing place)')
                WHEN rn.ObjectType = N'member'  THEN N'Member: ' + ISNULL(m.DisplayName, N'(missing member)')
                ELSE rn.ObjectType + N': ' + CONVERT(NVARCHAR(36), rn.ObjectId)
            END
    INTO ##GraphNodes
    FROM RawNodes rn
    LEFT JOIN dbo.pb_systems s
      ON rn.ObjectType = N'system' AND s.SystemId = rn.ObjectId
    LEFT JOIN #SystemEdgeLabelsInput sel
      ON rn.ObjectType = N'system' AND sel.SystemId = rn.ObjectId
    LEFT JOIN dbo.pb_groups g
      ON rn.ObjectType = N'group' AND g.GroupId = rn.ObjectId
    LEFT JOIN dbo.pb_places pl
      ON rn.ObjectType = N'place' AND pl.PlaceId = rn.ObjectId
    LEFT JOIN dbo.pb_members m
      ON rn.ObjectType = N'member' AND m.MemberId = rn.ObjectId;
END

GO


