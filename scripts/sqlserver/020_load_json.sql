/*
PluralBridge SQL Server script
020_load_json.sql

Loads exported JSON files into the local SQL Server database.

Before running this script:
1. Export JSON files locally.
2. Replace <local-export-folder> with the folder containing the JSON files.
3. Run this script in SQLCMD mode if using SSMS and master.sql.

This script contains no exported user data.
*/

USE PluralBridge;
GO

DECLARE @ExportFolder nvarchar(4000) = N'<local-export-folder>';

DECLARE @me_json              nvarchar(max);
DECLARE @members_json         nvarchar(max);
DECLARE @front_history_json   nvarchar(max);
DECLARE @customfields_json    nvarchar(max);
DECLARE @privacybuckets_json  nvarchar(max);
DECLARE @friends_json         nvarchar(max);
DECLARE @categories_json      nvarchar(max);
DECLARE @channels_json        nvarchar(max);

DECLARE @sql nvarchar(max);

DECLARE @path_me             nvarchar(4000) = @ExportFolder + N'\me.json';
DECLARE @path_members        nvarchar(4000) = @ExportFolder + N'\members.json';
DECLARE @path_front_history  nvarchar(4000) = @ExportFolder + N'\frontHistory.json';
DECLARE @path_customfields   nvarchar(4000) = @ExportFolder + N'\customFields.json';
DECLARE @path_privacybuckets nvarchar(4000) = @ExportFolder + N'\privacyBuckets.json';
DECLARE @path_friends        nvarchar(4000) = @ExportFolder + N'\friends.json';
DECLARE @path_categories     nvarchar(4000) = @ExportFolder + N'\categories.json';
DECLARE @path_channels       nvarchar(4000) = @ExportFolder + N'\channels.json';

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_me, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @me_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_members, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @members_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_front_history, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @front_history_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_customfields, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @customfields_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_privacybuckets, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @privacybuckets_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_friends, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @friends_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_categories, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @categories_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_channels, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @channels_json OUTPUT;

DECLARE @system_uid nvarchar(64);

SET @system_uid =
    COALESCE(
        JSON_VALUE(@me_json, '$.id'),
        JSON_VALUE(@me_json, '$.uid'),
        JSON_VALUE(@me_json, '$.content.id'),
        JSON_VALUE(@me_json, '$.content.uid')
    );

INSERT INTO dbo.[user]
(
    uid,
    name,
    raw_json
)
SELECT
    @system_uid,
    COALESCE(
        JSON_VALUE(@me_json, '$.content.name'),
        JSON_VALUE(@me_json, '$.name')
    ),
    @me_json
WHERE @system_uid IS NOT NULL;

INSERT INTO dbo.me
(
    uid,
    raw_json
)
SELECT
    @system_uid,
    @me_json
WHERE @system_uid IS NOT NULL;

INSERT INTO dbo.members
(
    id,
    system_uid,
    name,
    pronouns,
    description,
    avatar_url,
    avatar_uuid,
    raw_json
)
SELECT
    JSON_VALUE(j.value, '$.id') AS id,
    JSON_VALUE(j.value, '$.content.uid') AS system_uid,
    JSON_VALUE(j.value, '$.content.name') AS name,
    JSON_VALUE(j.value, '$.content.pronouns') AS pronouns,
    JSON_VALUE(j.value, '$.content.description') AS description,
    JSON_VALUE(j.value, '$.content.avatarUrl') AS avatar_url,
    JSON_VALUE(j.value, '$.content.avatarUuid') AS avatar_uuid,
    j.value AS raw_json
FROM OPENJSON(@members_json) AS j
WHERE JSON_VALUE(j.value, '$.id') IS NOT NULL;

INSERT INTO dbo.front_history
(
    id,
    member_id,
    start_time,
    end_time,
    raw_json
)
SELECT
    JSON_VALUE(j.value, '$.id') AS id,
    COALESCE(
        JSON_VALUE(j.value, '$.member'),
        JSON_VALUE(j.value, '$.memberId'),
        JSON_VALUE(j.value, '$.content.member'),
        JSON_VALUE(j.value, '$.content.memberId')
    ) AS member_id,
    TRY_CONVERT(bigint, COALESCE(
        JSON_VALUE(j.value, '$.startTime'),
        JSON_VALUE(j.value, '$.start_time'),
        JSON_VALUE(j.value, '$.content.startTime'),
        JSON_VALUE(j.value, '$.content.start_time')
    )) AS start_time,
    TRY_CONVERT(bigint, COALESCE(
        JSON_VALUE(j.value, '$.endTime'),
        JSON_VALUE(j.value, '$.end_time'),
        JSON_VALUE(j.value, '$.content.endTime'),
        JSON_VALUE(j.value, '$.content.end_time')
    )) AS end_time,
    j.value AS raw_json
FROM OPENJSON(@front_history_json) AS j
WHERE JSON_VALUE(j.value, '$.id') IS NOT NULL;

INSERT INTO dbo.customfields
(
    id,
    system_uid,
    name,
    type,
    raw_json
)
SELECT
    JSON_VALUE(j.value, '$.id') AS id,
    COALESCE(JSON_VALUE(j.value, '$.uid'), JSON_VALUE(j.value, '$.content.uid')) AS system_uid,
    COALESCE(JSON_VALUE(j.value, '$.name'), JSON_VALUE(j.value, '$.content.name')) AS name,
    COALESCE(JSON_VALUE(j.value, '$.type'), JSON_VALUE(j.value, '$.content.type')) AS type,
    j.value AS raw_json
FROM OPENJSON(@customfields_json) AS j
WHERE JSON_VALUE(j.value, '$.id') IS NOT NULL;

INSERT INTO dbo.privacybuckets
(
    id,
    system_uid,
    name,
    raw_json
)
SELECT
    JSON_VALUE(j.value, '$.id') AS id,
    COALESCE(JSON_VALUE(j.value, '$.uid'), JSON_VALUE(j.value, '$.content.uid')) AS system_uid,
    COALESCE(JSON_VALUE(j.value, '$.name'), JSON_VALUE(j.value, '$.content.name')) AS name,
    j.value AS raw_json
FROM OPENJSON(@privacybuckets_json) AS j
WHERE JSON_VALUE(j.value, '$.id') IS NOT NULL;

INSERT INTO dbo.friends
(
    id,
    system_uid,
    name,
    raw_json
)
SELECT
    JSON_VALUE(j.value, '$.id') AS id,
    COALESCE(JSON_VALUE(j.value, '$.uid'), JSON_VALUE(j.value, '$.content.uid')) AS system_uid,
    COALESCE(JSON_VALUE(j.value, '$.name'), JSON_VALUE(j.value, '$.content.name')) AS name,
    j.value AS raw_json
FROM OPENJSON(@friends_json) AS j
WHERE JSON_VALUE(j.value, '$.id') IS NOT NULL;

INSERT INTO dbo.chat_categories
(
    id,
    system_uid,
    name,
    raw_json
)
SELECT
    JSON_VALUE(j.value, '$.id') AS id,
    COALESCE(JSON_VALUE(j.value, '$.uid'), JSON_VALUE(j.value, '$.content.uid')) AS system_uid,
    COALESCE(JSON_VALUE(j.value, '$.name'), JSON_VALUE(j.value, '$.content.name')) AS name,
    j.value AS raw_json
FROM OPENJSON(@categories_json) AS j
WHERE JSON_VALUE(j.value, '$.id') IS NOT NULL;

INSERT INTO dbo.chat_channels
(
    id,
    category_id,
    system_uid,
    name,
    raw_json
)
SELECT
    JSON_VALUE(j.value, '$.id') AS id,
    COALESCE(JSON_VALUE(j.value, '$.category'), JSON_VALUE(j.value, '$.categoryId'), JSON_VALUE(j.value, '$.content.category')) AS category_id,
    COALESCE(JSON_VALUE(j.value, '$.uid'), JSON_VALUE(j.value, '$.content.uid')) AS system_uid,
    COALESCE(JSON_VALUE(j.value, '$.name'), JSON_VALUE(j.value, '$.content.name')) AS name,
    j.value AS raw_json
FROM OPENJSON(@channels_json) AS j
WHERE JSON_VALUE(j.value, '$.id') IS NOT NULL;
GO
