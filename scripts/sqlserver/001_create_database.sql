/*
PluralBridge SQL Server script
001_create_database.sql

Creates the optional local SQL Server database used for imported Simply Plural export data.
This script contains no exported user data.
*/

IF DB_ID(N'SimplyPlural') IS NULL
BEGIN
    CREATE DATABASE SimplyPlural;
END;
GO
