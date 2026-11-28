/*
PluralBridge SQL Server script
master.sql

Runs the SQL Server import/build scripts in order.

Run this from SQL Server Management Studio after reviewing paths and settings.
This script contains no exported user data.
*/

:r .\001_create_database.sql
:r .\010_create_tables.sql
:r .\020_load_json.sql
:r .\030_add_constraints.sql
:r .\040_create_views.sql
:r .\050_validation_queries.sql
:r .\060_report_queries.sql
