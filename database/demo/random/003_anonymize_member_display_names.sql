-- PluralBridgeDemoAnonXlat member display name anonymization
-- Generated from 49_random_womens_names.txt
-- Assigns names by deterministic MemberId order.
-- Run only from a query window connected directly to PluralBridgeDemoAnonXlat.

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. Connect directly to PluralBridgeDemoAnonXlat before running this script.', 1;
END;

-- Step 003 start

DECLARE @NameMap TABLE
(
    RowNum int NOT NULL PRIMARY KEY,
    NewDisplayName nvarchar(510) NOT NULL UNIQUE
);

INSERT INTO @NameMap (RowNum, NewDisplayName)
VALUES
    (1, N'Jesse'),
    (2, N'Bella'),
    (3, N'Xena'),
    (4, N'Beverley'),
    (5, N'Elena'),
    (6, N'Ann'),
    (7, N'Grace'),
    (8, N'Hannah'),
    (9, N'Rachel'),
    (10, N'Monica'),
    (11, N'Kiley'),
    (12, N'Luna'),
    (13, N'Carla'),
    (14, N'Nora'),
    (15, N'Tina'),
    (16, N'Penelope'),
    (17, N'Quinn'),
    (18, N'Riley'),
    (19, N'Sophia'),
    (20, N'Taylor'),
    (21, N'Uma'),
    (22, N'Fae'),
    (23, N'Willow'),
    (24, N'Ximena'),
    (25, N'Yara'),
    (26, N'Beth'),
    (27, N'Alice'),
    (28, N'Brielle'),
    (29, N'Camila'),
    (30, N'Daisy'),
    (31, N'Eloise'),
    (32, N'Freya'),
    (33, N'Gemma'),
    (34, N'Hazel'),
    (35, N'Isla'),
    (36, N'Jade'),
    (37, N'Kiara'),
    (38, N'Lily'),
    (39, N'Mila'),
    (40, N'Nova'),
    (41, N'Ophelia'),
    (42, N'Paige'),
    (43, N'Raelynn'),
    (44, N'Stella'),
    (45, N'Sheena'),
    (46, N'Valentina'),
    (47, N'Winter'),
    (48, N'Xyla'),
    (49, N'Zara');

IF (SELECT COUNT(*) FROM @NameMap) <> 49
BEGIN
    THROW 51001, 'Name map must contain exactly 49 rows.', 1;
END;

IF (SELECT COUNT(DISTINCT NewDisplayName) FROM @NameMap) <> 49
BEGIN
    THROW 51002, 'Name map must contain 49 distinct names.', 1;
END;

IF (SELECT COUNT(*) FROM dbo.pb_members) <> 49
BEGIN
    THROW 51003, 'pb_members must contain exactly 49 rows before name anonymization.', 1;
END;

DECLARE @MemberMap TABLE
(
    RowNum int NOT NULL PRIMARY KEY,
    MemberId uniqueidentifier NOT NULL UNIQUE
);

INSERT INTO @MemberMap (RowNum, MemberId)
SELECT
    ROW_NUMBER() OVER (ORDER BY MemberId) AS RowNum,
    MemberId
FROM dbo.pb_members;

IF (SELECT COUNT(*) FROM @MemberMap) <> 49
BEGIN
    THROW 51004, 'Member map must contain exactly 49 rows.', 1;
END;

BEGIN TRANSACTION;

UPDATE m
SET DisplayName = n.NewDisplayName
FROM dbo.pb_members AS m
INNER JOIN @MemberMap AS mm ON mm.MemberId = m.MemberId
INNER JOIN @NameMap AS n ON n.RowNum = mm.RowNum;

DECLARE @RowsUpdated int = @@ROWCOUNT;

IF @RowsUpdated <> 49
BEGIN
    THROW 51005, 'Expected to update exactly 49 member display names.', 1;
END;

IF (SELECT COUNT(*) FROM dbo.pb_members WHERE DisplayName IS NULL OR LTRIM(RTRIM(DisplayName)) = N'') <> 0
BEGIN
    THROW 51006, 'Member display-name anonymization produced blank or NULL names.', 1;
END;

IF (SELECT COUNT(DISTINCT DisplayName) FROM dbo.pb_members) <> 49
BEGIN
    THROW 51007, 'Member display-name anonymization did not produce 49 distinct names.', 1;
END;

COMMIT TRANSACTION;

SELECT
    mm.RowNum,
    m.MemberId,
    m.DisplayName
FROM dbo.pb_members AS m
INNER JOIN @MemberMap AS mm ON mm.MemberId = m.MemberId
ORDER BY mm.RowNum;

SELECT
    'PASS: updated 49 member display names from the supplied name list.' AS validation_result;

-- Step 003 end
