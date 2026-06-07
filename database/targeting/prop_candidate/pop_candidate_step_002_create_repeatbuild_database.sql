-- Step 89C start

USE [master];
GO

IF DB_ID(N'PluralBridge_RepeatBuild') IS NOT NULL
BEGIN
    ALTER DATABASE [PluralBridge_RepeatBuild]
        SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    DROP DATABASE [PluralBridge_RepeatBuild];
END;
GO

DBCC CLONEDATABASE (N'PluralBridge', N'PluralBridge_RepeatBuild');
GO

ALTER DATABASE [PluralBridge_RepeatBuild]
    SET READ_WRITE;
GO

ALTER DATABASE [PluralBridge_RepeatBuild]
    SET MULTI_USER;
GO

SELECT
    name,
    state_desc,
    user_access_desc,
    is_read_only
FROM sys.databases
WHERE name = N'PluralBridge_RepeatBuild';
GO

USE [PluralBridge_RepeatBuild];
GO

SELECT
    COUNT(*) AS PbTableCount
FROM sys.tables
WHERE name LIKE N'pb[_]%';
GO

-- Step 89C end
