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
DECLARE @privacybuckets_json  nvarchar(max) = N'[]';
DECLARE @friends_json         nvarchar(max) = N'[]';
DECLARE @categories_json      nvarchar(max) = N'[]';
DECLARE @channels_json        nvarchar(max) = N'[]';
DECLARE @manifest_json        nvarchar(max) = N'{}';

DECLARE @sql nvarchar(max);

DECLARE @path_me             nvarchar(4000) = @ExportFolder + N'\me.json';
DECLARE @path_members        nvarchar(4000) = @ExportFolder + N'\members.json';
DECLARE @path_front_history  nvarchar(4000) = @ExportFolder + N'\frontHistory.json';
DECLARE @path_customfields   nvarchar(4000) = @ExportFolder + N'\customFields.json';
DECLARE @path_privacybuckets nvarchar(4000) = @ExportFolder + N'\privacyBuckets.json';
DECLARE @path_friends        nvarchar(4000) = @ExportFolder + N'\friends.json';
DECLARE @path_categories     nvarchar(4000) = @ExportFolder + N'\categories.json';
DECLARE @path_channels       nvarchar(4000) = @ExportFolder + N'\channels.json';
DECLARE @path_manifest       nvarchar(4000) = @ExportFolder + N'\manifest.json';

PRINT 'Loading required JSON files';
GO

DECLARE @ExportFolder nvarchar(4000) = N'<local-export-folder>';

DECLARE @me_json              nvarchar(max);
DECLARE @members_json         nvarchar(max);
DECLARE @front_history_json   nvarchar(max);
DECLARE @customfields_json    nvarchar(max);
DECLARE @privacybuckets_json  nvarchar(max) = N'[]';
DECLARE @friends_json         nvarchar(max) = N'[]';
DECLARE @categories_json      nvarchar(max) = N'[]';
DECLARE @channels_json        nvarchar(max) = N'[]';
DECLARE @manifest_json        nvarchar(max) = N'{}';

DECLARE @sql nvarchar(max);

DECLARE @path_me             nvarchar(4000) = @ExportFolder + N'\me.json';
DECLARE @path_members        nvarchar(4000) = @ExportFolder + N'\members.json';
DECLARE @path_front_history  nvarchar(4000) = @ExportFolder + N'\frontHistory.json';
DECLARE @path_customfields   nvarchar(4000) = @ExportFolder + N'\customFields.json';
DECLARE @path_privacybuckets nvarchar(4000) = @ExportFolder + N'\privacyBuckets.json';
DECLARE @path_friends        nvarchar(4000) = @ExportFolder + N'\friends.json';
DECLARE @path_categories     nvarchar(4000) = @ExportFolder + N'\categories.json';
DECLARE @path_channels       nvarchar(4000) = @ExportFolder + N'\channels.json';
DECLARE @path_manifest       nvarchar(4000) = @ExportFolder + N'\manifest.json';

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_me, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @me_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_members, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @members_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_front_history, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @front_history_json OUTPUT;

SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_customfields, '''', '''''') + N''', SINGLE_CLOB) AS j;';
EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @customfields_json OUTPUT;

PRINT 'Loading optional JSON files when present';

BEGIN TRY
    SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_privacybuckets, '''', '''''') + N''', SINGLE_CLOB) AS j;';
    EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @privacybuckets_json OUTPUT;
END TRY
BEGIN CATCH
    PRINT 'Optional file not loaded: privacyBuckets.json';
    SET @privacybuckets_json = N'[]';
END CATCH;

BEGIN TRY
    SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_friends, '''', '''''') + N''', SINGLE_CLOB) AS j;';
    EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @friends_json OUTPUT;
END TRY
BEGIN CATCH
    PRINT 'Optional file not loaded: friends.json';
    SET @friends_json = N'[]';
END CATCH;

BEGIN TRY
    SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_categories, '''', '''''') + N''', SINGLE_CLOB) AS j;';
    EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @categories_json OUTPUT;
END TRY
BEGIN CATCH
    PRINT 'Optional file not loaded: categories.json';
    SET @categories_json = N'[]';
END CATCH;

BEGIN TRY
    SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_channels, '''', '''''') + N''', SINGLE_CLOB) AS j;';
    EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @channels_json OUTPUT;
END TRY
BEGIN CATCH
    PRINT 'Optional file not loaded: channels.json';
    SET @channels_json = N'[]';
END CATCH;


BEGIN TRY
    SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@path_manifest, '''', '''''') + N''', SINGLE_CLOB) AS j;';
    EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @manifest_json OUTPUT;
END TRY
BEGIN CATCH
    PRINT 'Optional file not loaded: manifest.json';
    SET @manifest_json = N'{}';
END CATCH;

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


PRINT 'Loading member notes from manifest mappings';

DROP TABLE IF EXISTS #note_manifest;

CREATE TABLE #note_manifest
(
    member_id nvarchar(64) NOT NULL,
    note_file nvarchar(260) NOT NULL,
    note_index int NULL,
    endpoint nvarchar(1000) NULL,
    ok bit NULL
);

INSERT INTO #note_manifest
(
    member_id,
    note_file,
    note_index,
    endpoint,
    ok
)
SELECT
    RIGHT(endpoint, CHARINDEX('/', REVERSE(endpoint)) - 1) AS member_id,
    REPLACE(filename, N'notes/', N'') AS note_file,
    TRY_CONVERT(int, REPLACE(REPLACE(filename, N'notes/', N''), N'.json', N'')) AS note_index,
    endpoint,
    TRY_CONVERT(bit, CASE WHEN ok_value = 'true' THEN 1 WHEN ok_value = 'false' THEN 0 ELSE NULL END) AS ok
FROM
(
    SELECT
        JSON_VALUE(j.value, '$.filename') AS filename,
        JSON_VALUE(j.value, '$.endpoint') AS endpoint,
        JSON_VALUE(j.value, '$.ok') AS ok_value
    FROM OPENJSON(@manifest_json, '$.files') AS j
) AS x
WHERE filename LIKE N'notes/%.json'
  AND endpoint LIKE N'/v1/notes/%/%'
  AND CHARINDEX('/', REVERSE(endpoint)) > 1;

DECLARE
    @member_id nvarchar(64),
    @note_file nvarchar(260),
    @note_index int,
    @endpoint nvarchar(1000),
    @ok bit,
    @note_json nvarchar(max),
    @note_path nvarchar(4000);

DECLARE note_cursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    member_id,
    note_file,
    note_index,
    endpoint,
    ok
FROM #note_manifest
ORDER BY note_index, note_file;

OPEN note_cursor;

FETCH NEXT FROM note_cursor
INTO @member_id, @note_file, @note_index, @endpoint, @ok;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @note_json = N'[]';
    SET @note_path = @ExportFolder + N'\..\notes\' + @note_file;

    BEGIN TRY
        SET @sql = N'SELECT @json_out = BulkColumn FROM OPENROWSET(BULK ''' + REPLACE(@note_path, '''', '''''') + N''', SINGLE_CLOB) AS j;';
        EXEC sys.sp_executesql @sql, N'@json_out nvarchar(max) OUTPUT', @json_out = @note_json OUTPUT;
    END TRY
    BEGIN CATCH
        PRINT 'Optional note file not loaded: ' + @note_file;
        SET @note_json = N'[]';
    END CATCH;

    INSERT INTO dbo.member_notes
    (
        member_id,
        note_file,
        note_index,
        endpoint,
        ok,
        raw_json
    )
    VALUES
    (
        @member_id,
        @note_file,
        @note_index,
        @endpoint,
        @ok,
        @note_json
    );

    FETCH NEXT FROM note_cursor
    INTO @member_id, @note_file, @note_index, @endpoint, @ok;
END

CLOSE note_cursor;
DEALLOCATE note_cursor;
GO
