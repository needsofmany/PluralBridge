# PluralBridge

Welcome to PluralBridge.

This project exists to help Simply Plural users preserve their own data and keep that data useful after the original service is no longer available.

PluralBridge is not trying to be a quick clone of Simply Plural.

PluralBridge is a preservation and continuity project. The first job is to get user data safely out while the original service still exists. The next job is to make that preserved data useful through local viewers, portable databases, cloud migration, compatible APIs, and future clients.

## Token Use Boundary

The Simply Plural API token is used only for exporting a user's own data from Simply Plural / Apparyllis while that service is still available.

The Simply Plural token is not a PluralBridge account credential.

The Simply Plural token will not be used for ongoing PluralBridge development, future PluralBridge services, future PluralBridge viewers, cloud access, mobile apps, REST services, or authentication after export.

PluralBridge will create and maintain its own authentication platform for any future hosted services, cloud-backed tools, REST APIs, or multi-device access.

## Independent Design Boundary

PluralBridge treats Simply Plural / Apparyllis as a source of user-owned export data, not as a product design template.

The project uses public API access only to help users export and preserve their own data while the original service is still available.

Future PluralBridge tools, viewers, clients, services, authentication systems, and user interfaces will be independently designed.

PluralBridge will not copy the Simply Plural user interface, application flow, branding, visual design, source code, mobile app code, website code, server code, or authentication system.

The long-term goal is to build a preservation and continuity platform around exported user data, with its own usability model, privacy model, storage targets, clients, and service architecture.

PluralBridge is an independent preservation and migration toolkit maintained by **Needs of the Many** (`@needsofmany`).

It helps users export, preserve, and bridge Simply Plural data into local JSON files, SQL databases, reports, viewers, converters, importers, and future tools.

## Independent Project

PluralBridge is an independent community preservation effort.

PluralBridge is not affiliated with Simply Plural, Apparyllis, or the Simply Plural development team.

The project uses public Apparyllis REST API endpoints with a user-created API token. It does not require reverse engineering, decompiling, disassembling, patching, intercepting, or modifying Simply Plural software.

## Current Status

PluralBridge is in an early preservation-focused stage.

The current repository contains documentation, safe examples, Python starter scripts, SQL Server script skeletons, and safety checks. The first priority is preserving user data locally in a way that keeps private data private.

## Public Website

The public PluralBridge website is live at:

- https://thepluralbridge.org
- https://www.thepluralbridge.org

The website source is stored in the `website/` directory of this repository.

The current site is deployed through Cloudflare Pages from the `master` branch, with `website` as the build output directory. It provides public-facing project information, Simply Plural export urgency, installation and run guidance, safety notes, and links back to this repository.

## Current Scope

The first release of PluralBridge focuses on preservation:

1. Exporting Simply Plural data through public Apparyllis REST API endpoints.
2. Saving exported data as local JSON files.
3. Downloading member avatar images where available.
4. Providing SQL Server scripts for loading exported JSON into relational tables.
5. Providing readable SQL views and validation queries.
6. Documenting token handling, privacy, file naming, and safe local storage.

Future work may include local viewers, converters, importers, reporting tools, and migration helpers.

## Repository Layout

    docs/                 Documentation
    examples/             Safe example configuration and redacted JSON shapes
    reports/              Report notes and examples
    scripts/bash/         Bash helper scripts
    scripts/python/       Python export helpers
    scripts/sqlserver/    SQL Server import scripts
    tests/                Future tests using synthetic or redacted data only
    website/              Public static website deployed through Cloudflare Pages

## Quick Start

Clone the repository:

    git clone https://github.com/needsofmany/PluralBridge.git
    cd PluralBridge

Set your Simply Plural API token in Git Bash:

    read -s -p "Paste Simply Plural token: " SP_TOKEN
    echo
    export SP_TOKEN

Check that a token was captured without printing it:

    printf 'Token length: %s\n' "${#SP_TOKEN}"

Export JSON data:

    python scripts/python/export_json.py --output-dir exports/json

Export avatar images after `members.json` has been exported:

    python scripts/python/export_avatars.py --members-json exports/json/members.json --output-dir exports/member_images

The `exports` folder is for local private data only. Do not commit it.

## Token Handling

Your Simply Plural API token is private. Treat it like a password.

Do not put tokens in:

- Git commits
- screenshots
- issues
- pull requests
- example files
- documentation
- shared chat rooms

PluralBridge expects the token in the `SP_TOKEN` environment variable.

The token is sent directly in the HTTP `Authorization` header. Do not add `Bearer` unless the official API documentation changes.

## Privacy and Safety

Exported Simply Plural data may contain deeply private information, including member names, notes, descriptions, pronouns, custom fields, fronting history, privacy buckets, friends data, avatar images, timestamps, and internal IDs.

Do not commit real exported data.

Avoid publishing:

- API tokens
- JSON exports
- notes
- avatar images
- SQL Server database backups
- generated reports containing private data
- screenshots containing private data

Use synthetic or heavily redacted examples.

## Repository Safety Check

Before committing or publishing changes, run:

    ./scripts/bash/check_repo_safety.sh

This checks tracked files for likely token leakage, private identity strings, private local paths, exported data folders, avatar images, note files, database files, and old SQL Server database-name references.

## Documentation

Key documentation files:

- `docs/api-token.md`
- `docs/avatar-export.md`
- `docs/data-model.md`
- `docs/developer-guide.md`
- `docs/install-and-run.md`
- `docs/json-export.md`
- `docs/notes-export.md`
- `docs/privacy-and-safety.md`
- `docs/public-api-use.md`
- `docs/regular-user-guide.md`
- `docs/roadmap.md`
- `docs/shutdown-and-preservation.md`
- `docs/sql-server-import.md`
- `docs/token-from-app.md`

## SQL Server

The optional SQL Server database name is:

    PluralBridge

The SQL Server scripts live under:

    scripts/sqlserver/

The planned script sequence is:

    001_create_database.sql
    010_create_tables.sql
    020_load_json.sql
    030_add_constraints.sql
    040_create_views.sql
    050_validation_queries.sql
    060_report_queries.sql
    master.sql

SQL scripts should remain data-agnostic. They must not contain real exported data, member names, notes, API tokens, private paths, or machine-specific folders.

## Python

Python helper scripts live under:

    scripts/python/

Current starter scripts:

    scripts/python/export_json.py
    scripts/python/export_avatars.py

Current helper package:

    scripts/python/pluralbridge/

The Python scripts should read tokens from `SP_TOKEN`, use relative output paths by default, avoid printing tokens, and keep exported data out of Git.

## Examples

Safe examples live under:

    examples/

Redacted JSON shape examples live under:

    examples/redacted-json-shapes/

These examples are synthetic or redacted. Do not replace them with real exported data.

## Reports

Report notes and examples live under:

    reports/

Reports should use synthetic examples or schema-level descriptions unless a user intentionally generates a private local report for their own use.

## Development Notes

Before committing:

1. Run the repository safety check.
2. Review `git status`.
3. Review staged file names.
4. Confirm no real exported data is staged.
5. Confirm no token or private path is staged.

Suggested checks:

    ./scripts/bash/check_repo_safety.sh
    git status
    git diff --cached --stat
    git diff --cached --name-only

## License

PluralBridge is licensed under the GNU General Public License v3.0.

Copyright (C) 2026 Needs of the Many

## Import Path for Other Tools

PluralBridge is intended to help the broader plural-tooling ecosystem, including other apps in this space.

Even projects that compete with PluralBridge's future viewers or clients may benefit from using PluralBridge as an import bridge for preserved Simply Plural data.

The immediate goal is to help users get their data out safely. A useful next goal is to make that preserved data easier for other tools to read, convert, import, and continue using.

## Export First, Refine Later

Run a preservation export as soon as you can.

Do not wait until your Simply Plural account is perfectly cleaned up, reorganized, renamed, edited, or updated before making a backup.

PluralBridge exports are safe to run multiple times while the Simply Plural service remains available. If you make changes in Simply Plural after one export, run PluralBridge again and create a newer export.

This matters because the most important preservation step is getting a copy of your data while export is still possible. A rough export made today is safer than a perfect export that never happens.

Recommended workflow:

1. Run an export now.
2. Keep that export private and backed up.
3. Make any Simply Plural edits or cleanup you want.
4. Run another export.
5. Keep the newest export, and optionally keep older exports as dated snapshots.

PluralBridge should be treated as a repeatable preservation tool. The export process is designed so users can capture data first, then rerun later as their Simply Plural data changes.

## Database Schema Diagram

A SQL Server schema diagram is available at:

    docs/images/PluralBridge-DB-Schema.png

The diagram shows the current tested SQL Server schema, including members, front history, notes, avatars, custom fields, privacy buckets, friends, and chat tables.
