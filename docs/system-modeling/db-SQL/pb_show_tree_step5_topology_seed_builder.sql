/****** Object:  StoredProcedure [dbo].[pb_show_tree_step5_topology_seed_builder]    Script Date: 10/26/2026 8:11:15 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


ALTER PROCEDURE [dbo].[pb_show_tree_step5_topology_seed_builder]
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

    IF OBJECT_ID('tempdb..##TopologyEdges') IS NOT NULL DROP TABLE ##TopologyEdges;
    IF OBJECT_ID('tempdb..##SystemEdgeLabels') IS NOT NULL DROP TABLE ##SystemEdgeLabels;

    SELECT
        p.ChildObjectType AS ObjectType,
        TRY_CONVERT(uniqueidentifier, p.ChildObjectId) AS ObjectId,
        ISNULL(p.PlacementRank, 999999) AS SortRank
    INTO #SeedPlacementRoots
    FROM dbo.pb_account_object_placements p
    WHERE p.AccountId = @AccountId
      AND p.ParentObjectType = N'account'
      AND p.ChildObjectType IN (N'group', N'system', N'place')
      AND TRY_CONVERT(uniqueidentifier, p.ChildObjectId) IS NOT NULL;

    SELECT
        sm.SystemId AS ObjectId,
        100000 + ROW_NUMBER() OVER (ORDER BY s.SystemName, sm.SystemId) AS SortRank
    INTO #SeedLegacySystemRoots
    FROM dbo.pb_system_memberships sm
    JOIN dbo.pb_system_membership_statuses sms
      ON sms.MembershipStatusId = sm.MembershipStatusId
    JOIN dbo.pb_systems s
      ON s.SystemId = sm.SystemId
    WHERE sm.AccountId = @AccountId
      AND sms.StatusName = N'Active';

    SELECT
        child.ObjectId AS ChildSystemId,
        parent.ObjectId AS ParentSystemId,
        ISNULL(r.RelationshipRank, 5000) AS EdgeRank
    INTO #SeedLegacySystemHierarchy
    FROM #SeedLegacySystemRoots child
    JOIN dbo.pb_system_relationships r
      ON r.SystemId = child.ObjectId
     AND r.TargetSystemId IS NULL
     AND r.ParentSystemId IS NOT NULL
    JOIN #SeedLegacySystemRoots parent
      ON parent.ObjectId = r.ParentSystemId;

    CREATE TABLE ##TopologyEdges
    (
        ParentObjectType NVARCHAR(32) NOT NULL,
        ParentObjectId UNIQUEIDENTIFIER NOT NULL,
        ChildObjectType NVARCHAR(32) NOT NULL,
        ChildObjectId UNIQUEIDENTIFIER NOT NULL,
        EdgeRank INT NOT NULL,
        EdgeSource NVARCHAR(64) NOT NULL
    );

    CREATE TABLE ##SystemEdgeLabels
    (
        SystemId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        EdgeLabel NVARCHAR(MAX) NULL
    );

    INSERT INTO ##TopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT
        p.ParentObjectType,
        TRY_CONVERT(uniqueidentifier, p.ParentObjectId),
        p.ChildObjectType,
        TRY_CONVERT(uniqueidentifier, p.ChildObjectId),
        ISNULL(p.PlacementRank, 999999),
        N'placement'
    FROM dbo.pb_account_object_placements p
    WHERE p.AccountId = @AccountId
      AND p.ParentObjectType IN (N'account', N'group', N'system', N'place')
      AND p.ChildObjectType IN (N'group', N'system', N'place')
      AND TRY_CONVERT(uniqueidentifier, p.ParentObjectId) IS NOT NULL
      AND TRY_CONVERT(uniqueidentifier, p.ChildObjectId) IS NOT NULL;

    INSERT INTO ##TopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
    SELECT
        N'system',
        h.ParentSystemId,
        N'system',
        h.ChildSystemId,
        h.EdgeRank,
        N'system-relationship'
    FROM #SeedLegacySystemHierarchy h
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM ##TopologyEdges e
        WHERE e.ParentObjectType = N'system'
          AND e.ParentObjectId = h.ParentSystemId
          AND e.ChildObjectType = N'system'
          AND e.ChildObjectId = h.ChildSystemId
    );

    ;WITH LabelParts AS
    (
        SELECT
            src.ObjectId AS SystemId,
            CustomLabel =
                NULLIF(
                    STUFF
                    (
                        (
                            SELECT N', ' + d.FieldName
                            FROM
                            (
                                SELECT DISTINCT cf2.FieldName
                                FROM dbo.pb_account_object_placements p2
                                JOIN dbo.pb_custom_fields cf2
                                  ON cf2.CustomFieldId = TRY_CONVERT(uniqueidentifier, p2.ChildObjectId)
                                 AND cf2.AccountId = p2.AccountId
                                WHERE p2.AccountId = @AccountId
                                  AND p2.ParentObjectType = N'system'
                                  AND p2.ChildObjectType = N'customField'
                                  AND TRY_CONVERT(uniqueidentifier, p2.ParentObjectId) = src.ObjectId
                                  AND cf2.FieldName IS NOT NULL
                            ) d
                            ORDER BY d.FieldName
                            FOR XML PATH(''), TYPE
                        ).value('.', 'nvarchar(max)')
                    ,1,2,N'')
                ,N''),
            RelationshipLabel =
                NULLIF(
                    STUFF
                    (
                        (
                            SELECT N', ' + d.RelType + N'->' + d.RelatedSystemName
                            FROM
                            (
                                SELECT DISTINCT
                                    ISNULL(NULLIF(r2.RelationshipType, N''), N'(type)') AS RelType,
                                    ISNULL(tgt.SystemName, N'(target)') AS RelatedSystemName
                                FROM dbo.pb_system_relationships r2
                                LEFT JOIN dbo.pb_systems tgt
                                  ON tgt.SystemId = r2.TargetSystemId
                                WHERE r2.SystemId = src.ObjectId
                                  AND r2.TargetSystemId IS NOT NULL

                                UNION ALL

                                SELECT DISTINCT
                                    ISNULL(NULLIF(r3.RelationshipType, N''), N'parent') AS RelType,
                                    ISNULL(par.SystemName, N'(parent)') AS RelatedSystemName
                                FROM dbo.pb_system_relationships r3
                                LEFT JOIN dbo.pb_systems par
                                  ON par.SystemId = r3.ParentSystemId
                                WHERE r3.SystemId = src.ObjectId
                                  AND r3.ParentSystemId IS NOT NULL
                            ) d
                            ORDER BY d.RelType, d.RelatedSystemName
                            FOR XML PATH(''), TYPE
                        ).value('.', 'nvarchar(max)')
                    ,1,2,N'')
                ,N'')
        FROM #SeedLegacySystemRoots src
    )
    INSERT INTO ##SystemEdgeLabels (SystemId, EdgeLabel)
    SELECT
        lp.SystemId,
        NULLIF(
            LTRIM(RTRIM(
                COALESCE(lp.CustomLabel, N'')
                + CASE
                    WHEN lp.CustomLabel IS NOT NULL AND lp.RelationshipLabel IS NOT NULL THEN N'; '
                    ELSE N''
                  END
                + COALESCE(lp.RelationshipLabel, N'')
            )),
        N'')
    FROM LabelParts lp;

    IF NOT EXISTS (SELECT 1 FROM #SeedPlacementRoots)
    BEGIN
        INSERT INTO ##TopologyEdges (ParentObjectType, ParentObjectId, ChildObjectType, ChildObjectId, EdgeRank, EdgeSource)
        SELECT
            N'account',
            @AccountId,
            N'system',
            l.ObjectId,
            l.SortRank,
            N'legacy-membership'
        FROM #SeedLegacySystemRoots l
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM #SeedLegacySystemHierarchy h
            WHERE h.ChildSystemId = l.ObjectId
        )
        AND NOT EXISTS
        (
            SELECT 1
            FROM ##TopologyEdges e
            WHERE e.ParentObjectType = N'account'
              AND e.ParentObjectId = @AccountId
              AND e.ChildObjectType = N'system'
              AND e.ChildObjectId = l.ObjectId
        );
    END;
END;

GO


