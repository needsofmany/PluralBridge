-- Step 7 start
IF NOT EXISTS (
    SELECT 1
    FROM dbo.pb_source_systems
    WHERE SourceSystemCode = N'APPARYLLIS'
)
BEGIN
    INSERT INTO dbo.pb_source_systems (
        SourceSystemCode,
        DisplayName,
        Description,
        ApiBaseUrl
    )
    VALUES (
        N'APPARYLLIS',
        N'Simply Plural / Apparyllis',
        N'Source adapter for preserved Simply Plural/Apparyllis API and export data.',
        N'https://api.apparyllis.com'
    );
END;

SELECT SourceSystemCode, DisplayName, Description, ApiBaseUrl
FROM dbo.pb_source_systems
WHERE SourceSystemCode = N'APPARYLLIS';
-- Step 7 end
