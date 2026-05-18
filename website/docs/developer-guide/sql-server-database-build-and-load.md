# PluralBridge SQL Server Database Build and Load Guide

Database creation, script map, JSON population sequence, reporting views, and validation workflow.

PluralBridge is published by Needs of the Many.

PluralBridge is independent and has no affiliation with Simply Plural, Apparyllis, or the Simply Plural development team.

## Purpose

This guide captures the repeatable procedure for building the PluralBridge SQL Server database and populating it from exported Simply Plural/Apparyllis JSON files. The workflow is staged: create the database and tables, bulk-load the exported JSON in dependency order, create readable reporting views, and run validation queries.

The SQL scripts are data-agnostic. They reference table names, JSON file names, JSON paths, and reporting views. They do not embed member names, field values, notes, fronting history details, or other Simply Plural personal content as literal values.

## Inputs and Assumptions

| Item | Value |
| --- | --- |
| SQL Server database name | `PluralBridge` |
| JSON export folder | `exports/json/` |
| Avatar manifest, if loading avatars | `exports/avatar_manifest.tsv` |
| SQL Server script folder | `scripts/sqlserver/` |
| Master orchestrator script | `scripts/sqlserver/master.sql` |

## SQL Server Feature Assumptions

- `OPENJSON`, `JSON_VALUE`, and `JSON_QUERY` are available.
- `OPENROWSET(BULK ...)` is allowed, and the SQL Server service account can read the export folder.
- `AT TIME ZONE` is available for UTC-to-Pacific reporting views.
- `STRING_AGG` is available for the member profile summary view. SQL Server 2017 or newer is recommended.

## Create the Empty Database and Schema

This phase creates the database, tables, keys, and base schema. No Simply Plural export data is inserted during this phase.

### Create or Select the Database

Create the empty database named `PluralBridge`, or select an already-created empty database intended for this import.

```sql
CREATE DATABASE PluralBridge;
GO

USE PluralBridge;
GO
```

### Apply the Schema Script

Run the schema creation script:


```text
scripts/sqlserver/010_create_tables.sql
```

This creates the tables used by the load scripts, including:

- `dbo.[user]`
- `dbo.me`
- `dbo.members`
- `dbo.front_history`
- `dbo.customfields`
- `dbo.member_info_values`
- `dbo.privacybuckets`
- `dbo.member_buckets`
- `dbo.chat_categories`
- `dbo.chat_channels`
- `dbo.friends`
- `dbo.member_avatars`
- `dbo.member_notes`

### Add Constraints

After the base tables exist and data loading has been proven, run the constraint script:


```text
scripts/sqlserver/030_add_constraints.sql
```

Foreign-key and constraint validation should match the load order defined in the numbered scripts.

### Confirm Table Columns


```sql
SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
```

### Confirm the Database Is Empty

Before loading, confirm that the target tables have zero rows. The master load script is designed for a fresh or empty schema.

```sql
SELECT
    s.name AS schema_name,
    t.name AS table_name,
    SUM(p.rows) AS row_count
FROM sys.tables t
JOIN sys.schemas s
    ON s.schema_id = t.schema_id
JOIN sys.partitions p
    ON p.object_id = t.object_id
WHERE p.index_id IN (0, 1)
  AND s.name = 'dbo'
GROUP BY s.name, t.name
ORDER BY t.name;
```

## Script Map

The current Simply Plural JSON-to-SQL Server pipeline uses numbered scripts under:

```text
scripts/sqlserver/
```

### Master Script

| Script | Use | Notes |
| --- | --- | --- |
| `master.sql` | Fresh empty database | Orchestrates the numbered scripts. Re-running against already-loaded tables can cause key errors or duplicates. |

### Numbered Scripts

| Script | Purpose |
| --- | --- |
| `001_create_database.sql` | Creates or selects the `PluralBridge` database. |
| `010_create_tables.sql` | Creates the base tables. |
| `020_load_json.sql` | Loads the exported JSON files into the core tables. |
| `030_add_constraints.sql` | Adds keys and constraints after the load structure is proven. |
| `040_create_views.sql` | Creates readable reporting views. |
| `050_validation_queries.sql` | Runs row count, referential, duplicate, and time-range validations. |
| `060_report_queries.sql` | Provides useful post-load report queries. |

## Populate the Database from JSON Files

This phase loads the export data. The cautious manual workflow is to examine the scripts and then run the numbered files in order. The master script can perform the load in one run after the process has been proven on redacted or safe test data.

### Recommended Run Order

1. Run `001_create_database.sql` if the database does not already exist.
2. Run `010_create_tables.sql`.
3. Run `020_load_json.sql` after confirming the JSON path and SQL Server file permissions.
4. Run `030_add_constraints.sql`.
5. Run `040_create_views.sql`.
6. Run `050_validation_queries.sql`.
7. Run `060_report_queries.sql`.

### Load Dependency Order

The load order follows the foreign-key and reporting dependency graph. Parent rows are inserted before child rows.


```text
dbo.[user]
    -> dbo.me
    -> dbo.members
    -> dbo.front_history
    -> dbo.member_info_values
    -> dbo.member_buckets
    -> dbo.privacybuckets
    -> dbo.customfields
    -> dbo.chat_categories
    -> dbo.chat_channels
    -> dbo.friends
    -> dbo.member_avatars
    -> dbo.member_notes
    -> reporting views
    -> validations
```

## Validation Baseline

The observed validation counts from a test import are useful as a reference for the same export set. They are not universal constants for future exports.

### Example Test-Load Counts

| Table | Example committed row count |
| --- | --- |
| `dbo.[user]` | 1 |
| `dbo.me` | 1 |
| `dbo.members` | 49 |
| `dbo.front_history` | 886 |
| `dbo.member_info_values` | 266 |
| `dbo.member_buckets` | 49 |
| `dbo.privacybuckets` | 2 |
| `dbo.customfields` | 7 |
| `dbo.chat_categories` | 1 |
| `dbo.chat_channels` | 6 |
| `dbo.friends` | 2 |

### Validation Checks

- Missing parent references should report all zero.
- Duplicate logical keys should report all zero.
- Front-history rows should not have end times before start times.
- A front-history row with a `NULL` end_time can represent a currently open front interval.

## Operating Notes

- The master script is best for a fresh, empty database after the process has been proven.
- The master script does not replace the exported JSON files. The JSON files remain the source evidence.
- The load scripts use `INSERT`, so re-running them against already-loaded tables can cause key errors or duplicates.
- Wrapper-style tables preserve content as JSON. They can be normalized later after the high-value relational tables are stable.
- Reporting views are non-destructive. They can be dropped and recreated without altering imported source data.
- The SQL Server database is a queryable representation of the exported files, not a substitute for keeping the original JSON export.

## Safety Notes

- Do not commit real export files, local database files, database backups, or connection strings.
- Do not use real member names, notes, avatar filenames, or fronting history details in public documentation.
- Use synthetic, redacted, or minimal example data for reports and tests.
- Any future public database documentation should point to a redacted fixture set or a toy export, not an actual user export.
