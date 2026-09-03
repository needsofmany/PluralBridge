# Roadmap

PluralBridge is an independent preservation and migration toolkit.

The first priority is preserving Simply Plural data locally in formats that future tools can use.

## Phase 1: Preservation Foundation

Goal: create a reliable developer-oriented preservation path.

Planned work:

- document API token handling
- document public API use
- export authenticated user/account data
- export members
- export fronting history
- export custom fields
- export privacy buckets
- export friends
- export notes where available
- download avatar images where available
- save raw API responses as local JSON
- use neutral filenames for notes and avatars
- keep exported private data out of Git

Expected output:

```text
exports/json/
exports/member_images/
exports/notes/
```

## Phase 2: SQL Server Import

Goal: make exported data queryable in a relational database.

Planned work:

- create SQL Server database scripts
- create relational tables
- load exported JSON files
- add primary keys and foreign keys
- add validation queries
- add readable views
- add report queries
- add avatar metadata support
- document expected script order

Expected SQL script set:

```text
001_create_database.sql
010_create_tables.sql
020_load_json.sql
030_add_constraints.sql
040_create_views.sql
050_validation_queries.sql
060_report_queries.sql
master.sql
```

Useful views:

```text
dbo.v_front_history_readable
dbo.v_front_history_pacific
dbo.v_current_front
dbo.v_member_info_readable
dbo.v_member_buckets_readable
dbo.v_member_profile_summary
dbo.v_member_profile_summary_with_avatar
```

## Phase 3: Cleaner Command-Line Tools

Goal: replace one-off shell commands with reusable scripts.

Planned work:

- create Python command-line export tools
- remove hard-coded local paths
- support command-line arguments
- optionally support ignored local config files
- read tokens from environment variables
- avoid printing secrets
- provide clear error messages
- validate JSON output
- summarize export results

Example command shape:

```bash
python scripts/python/export_json.py \
  --output-dir exports/json \
  --api-base https://api.apparyllis.com
```

Possible local config shape:

```json
{
  "api_base": "https://api.apparyllis.com",
  "output_dir": "./exports/json",
  "avatar_output_dir": "./exports/member_images",
  "token_env_var": "SP_TOKEN"
}
```

## Phase 4: Regular-User Documentation

Goal: make preservation possible for users who know how to install and run software, without assuming developer background.

Planned work:

- explain what an API token is
- explain how to generate a token from the app
- explain safe token handling
- provide copy-and-paste commands
- explain where exported files are saved
- explain what files should stay private
- provide screenshots where useful
- separate regular-user steps from developer notes
- avoid requiring SQL Server for basic preservation

Expected docs:

```text
docs/regular-user-guide.md
docs/api-token.md
docs/privacy-and-safety.md
docs/json-export.md
docs/avatar-export.md
docs/notes-export.md
```

## Phase 5: Reports and Local Viewing

Goal: make preserved data easier to inspect locally.

Possible work:

- static HTML reports
- member profile summaries
- front-history timeline reports
- current-front report
- avatar-aware reports
- CSV exports from SQL views
- small local viewer
- documentation for Access, Power BI, SSRS, or other local reporting tools

## Phase 6: Conversion and Migration Helpers

Goal: help users bridge preserved data into future tools.

Possible work:

- converters for selected JSON shapes
- import helpers for future community tools
- schema documentation
- mapping documents
- validation reports before migration
- export packages suitable for archiving

## Project Names

Possible future components:

```text
PluralBridge Export
PluralBridge SQL
PluralBridge Viewer
PluralBridge Reports
PluralBridge Import
PluralBridge Convert
```

## Guiding Principles

PluralBridge should:

- preserve first
- keep private data private
- use public API endpoints
- avoid software tampering
- avoid hard-coded personal paths
- use neutral filenames
- provide readable documentation
- support technical users first
- become easier for regular users over time
- keep the data useful for future tools

## Current Scope

The first public version is expected to be developer-centric.

That is acceptable. The immediate goal is to provide a clear, safe, auditable preservation path that others can test, improve, and build upon.

The public website is now live at `https://thepluralbridge.org`. It serves as the public-facing entry point for project information, Simply Plural export urgency, installation and run guidance, safety notes, and links back to the GitHub repository. Website source lives in the repository under `website/` and is deployed through Cloudflare Pages.
