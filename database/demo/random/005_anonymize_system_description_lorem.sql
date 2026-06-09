-- 005_anonymize_system_description_lorem.sql
-- Purpose: Replace dbo.pb_systems.Description with same-character-count lorem ipsum text.
-- Target database: PluralBridgeDemoAnonXlat
-- Azure SQL note: connect directly to PluralBridgeDemoAnonXlat before running. No USE statement.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. Connect directly to PluralBridgeDemoAnonXlat before running this script.', 1;
END;

IF OBJECT_ID(N'dbo.pb_systems', N'U') IS NULL
BEGIN
    THROW 51001, 'Expected table dbo.pb_systems was not found.', 1;
END;

IF COL_LENGTH(N'dbo.pb_systems', N'SystemId') IS NULL
BEGIN
    THROW 51002, 'Expected column dbo.pb_systems.SystemId was not found.', 1;
END;

IF COL_LENGTH(N'dbo.pb_systems', N'Description') IS NULL
BEGIN
    THROW 51003, 'Expected column dbo.pb_systems.Description was not found.', 1;
END;

IF (SELECT COUNT(*) FROM dbo.pb_systems) <> 1
BEGIN
    THROW 51004, 'Expected 1 row in dbo.pb_systems before anonymizing descriptions.', 1;
END;

DECLARE @LoremSeed nvarchar(max) = N'';
SET @LoremSeed = @LoremSeed + N'Lorem ipsum dolor sit amet, consectetur adipiscing elit. Quisque id mi sagittis, mollis tellus nec, vulputate lectus. Aliquam ac interdum nulla, nec tincidunt ante. Vestibulum id interdum nulla. Duis a bibendum leo. Phasellus eu dui ipsum. Suspendisse ultrices viverra mauris, ac euismod leo aliquam eget. Maecenas semper, sapien a aliquam gravida, metus magna dignissim tellus, sed vehicula lorem ex id dui. Curabitur eros erat, commodo eget mi et, sodales bibendum augue. Pellentesque quis ultrices lorem. Duis gravida, turpis eu porta sollicitudin, dui velit bibendum arcu, eget convallis purus diam non nisl. Sed sit amet pretium quam.
Ut id nisi fermentum diam consequat elementum. Vivamus mollis consequat tortor, vitae laoreet tellus tempus ut. Fusce placerat, mi a gravida mattis, ex nisl rutrum ipsum, sit amet vestibulum nulla ante id quam. Vivamus at tempus sapien, bibendum convallis nisi';
SET @LoremSeed = @LoremSeed + N'. Quisque dignissim ipsum non aliquam auctor. Maecenas purus tortor, accumsan ac est faucibus, volutpat viverra magna. Interdum et malesuada fames ac ante ipsum primis in faucibus. Duis at odio sagittis, aliquam erat vitae, ullamcorper lacus. Sed sed elementum ipsum, ac rutrum nunc. Etiam et nisl vitae nisl pellentesque consequat non quis tortor. Fusce lorem velit, sollicitudin a erat et, volutpat dapibus ex. Nam porta elementum massa nec volutpat. Aliquam non semper odio, eu dapibus nibh. Cras suscipit, velit ac tristique volutpat, lacus turpis tristique ex, eu suscipit lorem mauris a lorem. Etiam egestas suscipit imperdiet. Mauris scelerisque est quis hendrerit luctus.
Fusce enim tortor, congue in condimentum et, gravida a mauris. In fringilla est a quam vehicula, nec dictum orci maximus. Nam pellentesque metus lorem. Pellentesque ornare nisl vel pretium venenatis. Suspendisse potenti.';
SET @LoremSeed = @LoremSeed + N' Sed fermentum turpis eu rutrum pellentesque. Quisque rutrum nunc sit amet nisl tincidunt scelerisque. Nulla dictum non justo id dignissim. Donec laoreet hendrerit fringilla. Nulla ac pharetra augue. Maecenas ornare lacus vel metus faucibus rhoncus. Suspendisse ultricies consectetur interdum. Curabitur gravida vel justo ut euismod. Pellentesque eget condimentum enim, a aliquam velit.
Aenean sollicitudin fermentum mauris, sit amet efficitur augue egestas in. Praesent congue, purus et condimentum porta, ipsum odio tristique quam, non varius lorem felis et odio. In tempor ac enim laoreet ornare. Cras bibendum felis id pulvinar faucibus. Vestibulum sagittis tristique nunc. Sed in tellus sed massa feugiat vehicula. Cras vitae hendrerit libero. Donec porta convallis justo, ac vehicula nibh aliquam sit amet. Maecenas sodales eu diam quis egestas. Mauris blandit neque quis suscipit molestie. Sus';
SET @LoremSeed = @LoremSeed + N'pendisse ac luctus velit, eget tempor leo. Interdum et malesuada fames ac ante ipsum primis in faucibus. Suspendisse at nisi quis est tristique pretium et varius ante. Sed id nisi quis massa semper commodo in a ex. Proin fringilla porta risus ut faucibus.
Sed porttitor ornare augue sit amet pulvinar. Praesent porttitor neque velit, at varius nibh accumsan sed. Morbi iaculis non sem et elementum. Aenean maximus euismod nibh. Mauris at orci lorem. Phasellus vehicula nibh ut pellentesque commodo. Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae; Class aptent taciti sociosqu ad litora torquent per conubia nostra, per inceptos himenaeos. Sed ac scelerisque purus. Aliquam eget efficitur ex, in scelerisque nisi. Integer molestie orci at leo hendrerit tincidunt. In consequat, felis quis ullamcorper gravida, mi neque lobortis nulla, nec maximus lectus tortor ';
SET @LoremSeed = @LoremSeed + N'a mi. Pellentesque id consectetur felis. Aliquam erat volutpat. Donec sed massa libero.';

DECLARE @MaxDescriptionLength int;
SELECT @MaxDescriptionLength = ISNULL(MAX(DATALENGTH(CONVERT(nvarchar(max), [Description])) / 2), 0)
FROM dbo.pb_systems;

DECLARE @LoremText nvarchar(max) = N'';

WHILE DATALENGTH(@LoremText) / 2 < @MaxDescriptionLength
BEGIN
    SET @LoremText = @LoremText + @LoremSeed + N' ';
END;

DECLARE @Replacement TABLE
(
    SystemId uniqueidentifier NOT NULL PRIMARY KEY,
    OriginalLength int NULL,
    ReplacementDescription nvarchar(max) NULL
);

INSERT INTO @Replacement
(
    SystemId,
    OriginalLength,
    ReplacementDescription
)
SELECT
    SystemId,
    DATALENGTH(CONVERT(nvarchar(max), [Description])) / 2 AS OriginalLength,
    CASE
        WHEN [Description] IS NULL THEN NULL
        ELSE LEFT(@LoremText, DATALENGTH(CONVERT(nvarchar(max), [Description])) / 2)
    END AS ReplacementDescription
FROM dbo.pb_systems;

BEGIN TRANSACTION;

UPDATE s
SET [Description] = r.ReplacementDescription
FROM dbo.pb_systems AS s
JOIN @Replacement AS r
    ON r.SystemId = s.SystemId;

DECLARE @RowsUpdated int = @@ROWCOUNT;

IF @RowsUpdated <> 1
BEGIN
    THROW 51005, 'Description anonymization did not update exactly 1 system row.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_systems AS s
    JOIN @Replacement AS r
        ON r.SystemId = s.SystemId
    WHERE
        (s.[Description] IS NULL AND r.ReplacementDescription IS NOT NULL)
        OR (s.[Description] IS NOT NULL AND r.ReplacementDescription IS NULL)
        OR (s.[Description] <> r.ReplacementDescription)
)
BEGIN
    THROW 51006, 'Description anonymization verification failed.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.pb_systems AS s
    JOIN @Replacement AS r
        ON r.SystemId = s.SystemId
    WHERE
        (s.[Description] IS NOT NULL)
        AND (DATALENGTH(CONVERT(nvarchar(max), s.[Description])) / 2) <> r.OriginalLength
)
BEGIN
    THROW 51007, 'Description anonymization changed one or more non-null description character counts.', 1;
END;

COMMIT TRANSACTION;

SELECT
    'PASS: system descriptions replaced with same-character-count lorem ipsum text.' AS validation_result,
    COUNT(*) AS pb_systems,
    SUM(CASE WHEN [Description] IS NULL THEN 1 ELSE 0 END) AS null_descriptions,
    SUM(CASE WHEN [Description] IS NOT NULL THEN 1 ELSE 0 END) AS lorem_descriptions,
    MIN(DATALENGTH(CONVERT(nvarchar(max), [Description])) / 2) AS min_description_chars,
    MAX(DATALENGTH(CONVERT(nvarchar(max), [Description])) / 2) AS max_description_chars
FROM dbo.pb_systems;
