# SQL Server Import

PluralBridge can load exported Simply Plural JSON files into SQL Server.

The SQL Server path is optional. The first preservation layer is the local JSON export. SQL Server makes the data easier to validate, query, join, report, and inspect.

## Requirements

You need:

- SQL Server
- SQL Server Management Studio or another SQL client
- exported Simply Plural JSON files
- SQL scripts from `scripts/sqlserver/`

## Recommended Local Export Folder

Examples may refer to this local JSON folder:

```text
<local-export-folder>
```

For public scripts and documentation, prefer configurable paths or clearly marked examples.

Do not commit real exported JSON files.

## Database Name

Default database name used by PluralBridge examples:

```text
PluralBridge
```

## Suggested Script Order

Run scripts in this general order:

```text
001_create_database.sql
010_create_tables.sql
020_load_json.sql
030_add_constraints.sql
040_create_views.sql
050_validation_queries.sql
060_report_queries.sql
```

A convenience wrapper may also be provided:

```text
master.sql
```

## Script Responsibilities

### 001_create_database.sql

Creates the database if it does not already exist.

Expected database:

```text
PluralBridge
```

### 010_create_tables.sql

Creates relational tables for exported Simply Plural data.

Core tables may include:

```text
dbo.[user]
dbo.me
dbo.members
dbo.front_history
dbo.customfields
dbo.member_info_values
dbo.privacybuckets
dbo.member_buckets
dbo.chat_categories
dbo.chat_channels
dbo.friends
dbo.member_avatars
```

### 020_load_json.sql

Loads exported JSON files into SQL Server tables.

This script may use `OPENROWSET`, `OPENJSON`, staging tables, or direct inserts depending on the final implementation.

Expected source files may include:

```text
me.json
members.json
frontHistory.json
customFields.json
privacyBuckets.json
categories.json
channels.json
friends.json
```

Empty JSON files should be handled safely.

### 030_add_constraints.sql

Adds primary keys, foreign keys, uniqueness checks, and supporting indexes after data load.

Useful relationships include:

```text
members.system_uid -> user.uid
me.uid -> user.uid
front_history.member_id -> members.id
member_info_values.member_id -> members.id
member_info_values.field_id -> customfields.id
member_buckets.member_id -> members.id
member_buckets.bucket_id -> privacybuckets.id
```

### 040_create_views.sql

Creates readable views for inspection and reports.

Useful views may include:

```text
dbo.v_front_history_readable
dbo.v_front_history_pacific
dbo.v_current_front
dbo.v_member_info_readable
dbo.v_member_buckets_readable
dbo.v_member_profile_summary
dbo.v_member_profile_summary_with_avatar
```

### 050_validation_queries.sql

Runs validation checks.

Useful checks include:

```text
row counts
missing parent records
duplicate keys
front history records with missing start_time
front history records with end_time before start_time
front history records with NULL end_time
```

A `NULL` `end_time` can be valid when a member is currently fronting.

### 060_report_queries.sql

Provides readable example queries.

Useful reports include:

```text
current front
fronting count by member
recent front history
member profile summary
custom field summary
member bucket summary
members with no fronting history
members with avatar images
members without avatar images
```

## Time Conversion

Simply Plural front-history timestamps may be stored as Unix timestamps in milliseconds.

SQL Server conversion to UTC typically uses:

```sql
DATEADD(MILLISECOND, timestamp_ms % 1000,
    DATEADD(SECOND, timestamp_ms / 1000, '19700101'))
```

Pacific time conversion can use:

```sql
utc_datetime AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time'
```

For reliable view definitions, compute UTC first, then convert UTC to Pacific time.

## Avatar Metadata

Avatar image files are usually stored on disk, not rendered directly inside SSMS result grids.

A practical SQL table stores metadata:

```text
member_id
system_uid
avatar_uuid
source_url
local_filename
local_path
downloaded_at
```

A readable view can expose the local file path or file URL for reports and local viewers.

## Privacy Warning

A SQL Server database created from Simply Plural exports may contain private system data.

This can include:

- member names
- descriptions
- custom fields
- notes
- fronting history
- friends
- avatar paths
- readable reports and views

Do not publish:

```text
*.bak
*.mdf
*.ldf
*.csv
*.tsv
*.xlsx
screenshots of result grids
query results containing private data
```

unless the material has been reviewed and intentionally redacted.

## Before Publishing Scripts

SQL scripts should be data-agnostic.

They should not contain:

- real member names
- real note text
- real API tokens
- real private descriptions
- real exported JSON payloads

Environment-specific paths are acceptable when clearly marked as examples, but public scripts should prefer variables, SQLCMD parameters, or documented edits.

## Validation After Import

After loading, confirm:

```text
expected row counts
zero missing parent records
zero unexpected duplicate keys
current front rows are understood
time conversions are correct
views return readable data
```

For front-history records, a single open interval with `end_time IS NULL` may be correct if the member is still fronting.

## Recommended Workflow

1. Export JSON locally.
2. Inspect file presence and size.
3. Create the SQL Server database.
4. Create tables.
5. Load JSON.
6. Validate row counts.
7. Add constraints.
8. Create views.
9. Run validation queries.
10. Run report queries.
11. Keep database backups private.

## Database Name

The public PluralBridge SQL Server database name is:

    PluralBridge

Earlier private development may have used a local database named `SimplyPlural`. That name should be treated as a private development artifact only.

For public use, testing, documentation, and scripts, use `PluralBridge`.

A safe migration path is:

1. Keep any existing private `SimplyPlural` database as a reference copy.
2. Create a fresh `PluralBridge` database from the public scripts.
3. Load exported JSON into `PluralBridge`.
4. Run the validation queries.
5. Compare expected counts and reports against the known-good local reference, if one exists.
6. Delete or archive the older private database only after `PluralBridge` has passed validation.

## Raw JSON and Future Normalization

PluralBridge stores selected high-value fields in relational columns and also preserves full source rows in `raw_json` columns.

This is intentional.

The first public release prioritizes preservation, safe loading, readable reports, and keeping enough original payload information to improve the schema later.

Some API response fields may remain embedded inside `raw_json` during the initial release. Future versions may add more normalized tables and columns as additional response shapes are confirmed.

Examples of possible future normalization work include:

- additional member profile fields
- nested custom field structures
- notes metadata
- chat-related structures
- timer-related structures
- poll-related structures
- any other repeated embedded objects found across exports

The initial SQL Server schema should be considered a preservation-first schema, not a final analytical warehouse.

## Tested Import Coverage

The SQL Server import path has been tested against a live export using the `PluralBridge` database name.

The tested import path loads:

    user.json
    me.json
    members.json
    frontHistory.json
    fronthistory_starttime_and_endtime.json
    customFields.json
    privacyBuckets.json
    friends.json
    categories.json
    channels.json
    manifest.json
    exports/notes/*.json
    avatar_manifest.tsv

The tested SQL tables include:

    dbo.[user]
    dbo.me
    dbo.members
    dbo.front_history
    dbo.customfields
    dbo.privacybuckets
    dbo.friends
    dbo.chat_categories
    dbo.chat_channels
    dbo.member_notes
    dbo.member_avatars

The `member_notes` table is populated from `manifest.json` and the neutral note files under:

    exports/notes

The `member_avatars` table is populated from:

    exports/avatar_manifest.tsv

This allows notes and avatar files to be joined back to members without exposing member names in exported filenames.

## Tested Local Export Layout

The tested local export layout is:

    exports/json
    exports/notes
    exports/member_images
    exports/avatar_manifest.tsv

The `exports` folder is private local output and must not be committed.

## Schema Diagram

A SQL Server schema diagram is available at:

    docs/images/PluralBridge-DB-Schema.png

Use this diagram as a visual companion to the numbered SQL Server scripts. It reflects the tested import path and the current preservation-first schema, including member notes and avatar metadata.
