-- Restore dbo.pb_show_tree_step5_topology_seed_builder to the definition
-- that writes the caller's #TopologyEdges and #SystemEdgeLabels.
-- This script has not been executed.
-- Run it only after 012_profile_seed_result_capture_recovery.sql, and
-- only while no caller still captures the seed result set.
-- The procedure text below is the repository procedure from before
-- 011_topology_seed_result_set.sql. It does not create permanent tables.
-- GO starts a new batch, so a THROW before GO does not stop ALTER
-- PROCEDURE. A failed guard turns NOEXEC on. Later batches, including
-- ALTER PROCEDURE, are compiled and not executed. SET NOEXEC OFF still
-- runs and restores the session.

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    RAISERROR('Stop: set the query window database dropdown to PluralBridgeChap2SafeSpine.', 16, 1);
    SET NOEXEC ON;
END
GO

IF OBJECT_ID(N'dbo.pb_show_tree_step5_topology_seed_builder', N'P') IS NULL
BEGIN
    RAISERROR('Stop: dbo.pb_show_tree_step5_topology_seed_builder is missing.', 16, 1);
    SET NOEXEC ON;
END
GO

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
        100000 + ROW_NUMBER() OVER
            (ORDER BY s.SystemName, sm.SystemId) AS SortRank
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
        FROM #TopologyEdges e
        WHERE e.ParentObjectType = N'system'
          AND e.ParentObjectId = h.ParentSystemId
          AND e.ChildObjectType = N'system'
          AND e.ChildObjectId = h.ChildSystemId
    );

    -- Relationship labels come exclusively from relationship rows.
    -- Custom-field ownership scopes these labels to the selected account.
    ;WITH RelationshipLabels AS
    (
        SELECT
            r.AnchorRelationshipId,
            r.SourceObjectId AS SystemId,
            r.TargetObjectType,
            r.TargetObjectId,
            cf.CustomFieldId,
            cf.FieldName AS LabelText,
            COALESCE
            (
                targetSystem.SystemName,
                targetGroup.GroupName,
                targetPlace.PlaceName,
                N'(target)'
            ) AS TargetName
        FROM dbo.pb_anchor_relationships AS r
        INNER JOIN dbo.pb_custom_fields AS cf
            ON cf.CustomFieldId = r.CustomFieldId
           AND cf.AccountId = @AccountId
        LEFT JOIN dbo.pb_systems AS targetSystem
            ON r.TargetObjectType = N'system'
           AND targetSystem.SystemId = r.TargetObjectId
        LEFT JOIN dbo.pb_groups AS targetGroup
            ON r.TargetObjectType = N'group'
           AND targetGroup.GroupId = r.TargetObjectId
        LEFT JOIN dbo.pb_places AS targetPlace
            ON r.TargetObjectType = N'place'
           AND targetPlace.PlaceId = r.TargetObjectId
        WHERE r.SourceObjectType = N'system'
    ),
    RelationshipGroups AS
    (
        SELECT DISTINCT
            SystemId,
            TargetObjectType,
            TargetObjectId,
            TargetName
        FROM RelationshipLabels
    ),
    GroupLabels AS
    (
        SELECT
            g.SystemId,
            g.TargetObjectType,
            g.TargetObjectId,
            g.TargetName,
            LabelText = STUFF
            (
                (
                    SELECT N', ' + r.LabelText
                    FROM RelationshipLabels AS r
                    WHERE r.SystemId = g.SystemId
                      AND
                      (
                          r.TargetObjectType = g.TargetObjectType
                          OR
                          (
                              r.TargetObjectType IS NULL
                              AND g.TargetObjectType IS NULL
                          )
                      )
                      AND
                      (
                          r.TargetObjectId = g.TargetObjectId
                          OR
                          (
                              r.TargetObjectId IS NULL
                              AND g.TargetObjectId IS NULL
                          )
                      )
                    ORDER BY
                        r.LabelText,
                        r.CustomFieldId,
                        r.AnchorRelationshipId
                    FOR XML PATH(''), TYPE
                ).value('.', 'nvarchar(max)'),
                1, 2, N''
            )
        FROM RelationshipGroups AS g
    )
    INSERT INTO #SystemEdgeLabels (SystemId, EdgeLabel)
    SELECT
        sources.SystemId,
        STUFF
        (
            (
                SELECT
                    N'; ' + labels.LabelText
                    + CASE
                        WHEN labels.TargetObjectId IS NULL THEN N''
                        ELSE N'->' + labels.TargetName
                      END
                FROM GroupLabels AS labels
                WHERE labels.SystemId = sources.SystemId
                ORDER BY
                    labels.TargetObjectType,
                    labels.TargetObjectId
                FOR XML PATH(''), TYPE
            ).value('.', 'nvarchar(max)'),
            1, 2, N''
        )
    FROM
    (
        SELECT DISTINCT SystemId
        FROM RelationshipLabels
    ) AS sources;

    IF NOT EXISTS (SELECT 1 FROM #SeedPlacementRoots)
    BEGIN
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
            FROM #TopologyEdges e
            WHERE e.ParentObjectType = N'account'
              AND e.ParentObjectId = @AccountId
              AND e.ChildObjectType = N'system'
              AND e.ChildObjectId = l.ObjectId
        );
    END;
END;
GO

SET NOEXEC OFF;
GO
