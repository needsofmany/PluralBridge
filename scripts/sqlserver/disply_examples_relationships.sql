;WITH Anchors AS
(
    SELECT
        N'system' AS AnchorType,
        SystemId AS AnchorId,
        SystemName AS AnchorName
    FROM dbo.pb_systems

    UNION ALL

    SELECT N'group', GroupId, GroupName
    FROM dbo.pb_groups

    UNION ALL

    SELECT N'place', PlaceId, PlaceName
    FROM dbo.pb_places
)
SELECT
    a.AccountId,
    a.Username,
    cf.CustomFieldId,
    cf.FieldName,
    anchor.AnchorType,
    anchor.AnchorId,
    anchor.AnchorName
FROM dbo.pb_accounts AS a
LEFT JOIN dbo.pb_custom_fields AS cf
    ON cf.AccountId = a.AccountId
   AND cf.FieldName IN
   (
       N'sibling',
       N'related',
       N'evolving',
       N'unknown',
       N'archived',
       N'associated'
   )
LEFT JOIN dbo.pb_account_object_placements AS p
    ON p.AccountId = cf.AccountId
   AND p.ChildObjectType = N'customField'
   AND TRY_CONVERT(uniqueidentifier, p.ChildObjectId)
       = cf.CustomFieldId
LEFT JOIN Anchors AS anchor
    ON anchor.AnchorType = p.ParentObjectType
   AND anchor.AnchorId =
       TRY_CONVERT(uniqueidentifier, p.ParentObjectId)
WHERE a.Username IN
(
    N'example-1',  N'example-2',  N'example-3',
    N'example-4',  N'example-5',  N'example-6',
    N'example-7',  N'example-8',  N'example-9',
    N'example-10', N'example-11', N'example-12'
)
ORDER BY
    a.Username,
    cf.FieldName,
    cf.CustomFieldId,
    anchor.AnchorName;