-- Canonical definition of dbo.pb_show_tree_step5_topology_seed_builder.
-- Deploy from scripts/sqlserver/Members/011_topology_seed_result_set.sql.
-- This procedure does not write the caller's #TopologyEdges or #SystemEdgeLabels.
-- It returns one result set. The caller copies that set into those tables.
-- The result column order is the @SeedRows contract.

USE [PluralBridgeChap2SafeSpine];
GO

SET ANSI_NULLS ON;
GO

SET QUOTED_IDENTIFIER ON;
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

    CREATE TABLE #BuiltTopologyEdges
    (
        ParentObjectType nvarchar(32) NOT NULL,
        ParentObjectId uniqueidentifier NOT NULL,
        ChildObjectType nvarchar(32) NOT NULL,
        ChildObjectId uniqueidentifier NOT NULL,
        EdgeRank int NOT NULL,
        EdgeSource nvarchar(64) NOT NULL
    );

    CREATE TABLE #BuiltSystemEdgeLabels
    (
        SystemId uniqueidentifier NOT NULL PRIMARY KEY,
        EdgeLabel nvarchar(max) NULL
    );

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

    INSERT INTO #BuiltTopologyEdges
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

    INSERT INTO #BuiltTopologyEdges
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
        FROM #BuiltTopologyEdges e
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
    INSERT INTO #BuiltSystemEdgeLabels (SystemId, EdgeLabel)
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
        INSERT INTO #BuiltTopologyEdges
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
            FROM #BuiltTopologyEdges e
            WHERE e.ParentObjectType = N'account'
              AND e.ParentObjectId = @AccountId
              AND e.ChildObjectType = N'system'
              AND e.ChildObjectId = l.ObjectId
        );
    END;

    -- One result set. Edge rows and label rows share this column list.
    -- Null placeholders are typed so the union does not shorten nvarchar values.
    SELECT
        CAST(N'edge' AS nvarchar(8)) AS RowKind,
        ParentObjectType,
        ParentObjectId,
        ChildObjectType,
        ChildObjectId,
        EdgeRank,
        EdgeSource,
        CAST(NULL AS uniqueidentifier) AS SystemId,
        CAST(NULL AS nvarchar(max)) AS EdgeLabel
    FROM #BuiltTopologyEdges
    UNION ALL
    SELECT
        CAST(N'label' AS nvarchar(8)) AS RowKind,
        CAST(NULL AS nvarchar(32)) AS ParentObjectType,
        CAST(NULL AS uniqueidentifier) AS ParentObjectId,
        CAST(NULL AS nvarchar(32)) AS ChildObjectType,
        CAST(NULL AS uniqueidentifier) AS ChildObjectId,
        CAST(NULL AS int) AS EdgeRank,
        CAST(NULL AS nvarchar(64)) AS EdgeSource,
        SystemId,
        EdgeLabel
    FROM #BuiltSystemEdgeLabels;
END;
GO
