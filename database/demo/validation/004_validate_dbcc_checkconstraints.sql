SET NOCOUNT ON;

IF DB_NAME() <> N'PluralBridgeDemoAnonXlat'
BEGIN
    THROW 51000, 'Wrong database. This validation script must run only against PluralBridgeDemoAnonXlat.', 1;
END

PRINT '004 DBCC constraint validation';

CREATE TABLE #constraint_violations
(
    [Table] nvarchar(512) NULL,
    [Constraint] nvarchar(512) NULL,
    [Where] nvarchar(max) NULL
);

INSERT INTO #constraint_violations
EXEC ('DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS, NO_INFOMSGS');

SELECT * FROM #constraint_violations;

IF EXISTS (SELECT 1 FROM #constraint_violations)
BEGIN
    THROW 51007, 'DBCC CHECKCONSTRAINTS reported one or more violations.', 1;
END

SELECT 'PASS: DBCC CHECKCONSTRAINTS returned no violations.' as Validation_Result;
