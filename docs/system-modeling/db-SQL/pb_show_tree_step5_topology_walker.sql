/****** Object:  StoredProcedure [dbo].[pb_show_tree_step5_topology_walker]    Script Date: 10/28/2026 12:40:27 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

ALTER   PROCEDURE [dbo].[pb_show_tree_step5_topology_walker]
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

    IF OBJECT_ID('tempdb..#TopologyEdges') IS NULL AND OBJECT_ID('tempdb..##TopologyEdges') IS NULL
    BEGIN
        RAISERROR('#TopologyEdges or ##TopologyEdges must exist before topology walker.', 16, 1);
        RETURN;
    END;

    DECLARE @SrcTopologyEdges TABLE
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ChildObjectType NVARCHAR(32) NOT NULL,
        ChildObjectId UNIQUEIDENTIFIER NOT NULL,
        EdgeRank INT NOT NULL,
        EdgeSource NVARCHAR(64) NOT NULL
    );

    IF OBJECT_ID('tempdb..#TopologyEdges') IS NOT NULL
    BEGIN
        INSERT INTO @SrcTopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
        SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
        FROM #TopologyEdges;
    END
    ELSE
    BEGIN
        INSERT INTO @SrcTopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
        SELECT ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource
        FROM ##TopologyEdges;
    END;

    IF OBJECT_ID('tempdb..##WalkTopology') IS NOT NULL DROP TABLE ##WalkTopology;

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
        FROM @SrcTopologyEdges e
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
                w.PathKey
                + N'.'
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
        FROM @SrcTopologyEdges e
        JOIN Walk w
          ON e.ParentObjectType = w.ObjectType
         AND e.ParentObjectId = w.ObjectId
        WHERE w.Visited NOT LIKE N'%|' + e.ChildObjectType + N':' + CONVERT(NVARCHAR(36), e.ChildObjectId) + N'|%'
    )
    SELECT ParentObjectType, ParentObjectId, ObjectType, ObjectId, Depth, PathKey
    INTO ##WalkTopology
    FROM Walk
    OPTION (MAXRECURSION 200);
END;

GO


