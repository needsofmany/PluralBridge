DECLARE @ScriptTitle NVARCHAR(200) = N'Account.8 Inspect account status lookup';

IF DB_NAME() <> N'PluralBridgeChap2SafeSpine'
BEGIN
    DECLARE @WrongDatabaseMessage NVARCHAR(400) = @ScriptTitle + N': wrong database selected.';
    THROW 51000, @WrongDatabaseMessage, 1;
END;

SELECT
    @ScriptTitle AS ScriptTitle,
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id AS ColumnId,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.precision AS Precision,
    c.scale AS Scale,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.schemas s
    ON s.schema_id = t.schema_id
INNER JOIN sys.columns c
    ON c.object_id = t.object_id
INNER JOIN sys.types ty
    ON ty.user_type_id = c.user_type_id
WHERE t.name LIKE N'%account%'
  AND t.name LIKE N'%status%'
ORDER BY
    s.name,
    t.name,
    c.column_id;

SELECT
    @ScriptTitle AS ScriptTitle,
    s.name AS SchemaName,
    t.name AS TableName
FROM sys.tables t
INNER JOIN sys.schemas s
    ON s.schema_id = t.schema_id
WHERE t.name LIKE N'%account%'
  AND t.name LIKE N'%status%'
ORDER BY
    s.name,
    t.name;