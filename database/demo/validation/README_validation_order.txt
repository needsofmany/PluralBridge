PluralBridgeDemoAnonXlat validation scripts
Generated from uploaded SSMS master script: script_all_guids_remapped.sql
Target database: PluralBridgeDemoAnonXlat

Run in SSMS in this order:

1. 001_validate_row_counts.sql
   Confirms all table row counts match the generated master script.

2. 002_validate_table_column_shape.sql
   Confirms expected dbo tables, columns, data types, and nullability.

3. 003_validate_keys_constraints_inventory.sql
   Confirms primary keys, foreign keys, unique constraints, default constraints, and FK trust/enabled status.

4. 004_validate_dbcc_checkconstraints.sql
   Runs DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS. Empty result set means no violations.

5. 005_validate_foreign_key_orphans.sql
   Independently checks every actual FK for orphan rows using metadata, not guessed column names.

6. 006_profile_uniqueidentifier_columns.sql
   Read-only GUID profile. This is informational, not a pass/fail anonymization audit.

All scripts start with USE [PluralBridgeDemoAnonXlat] and a database-name guard.
All scripts are read-only.
