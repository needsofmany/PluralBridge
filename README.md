# PluralBridge

PluralBridge is not trying to be a quick clone of Simply Plural.

PluralBridge is a preservation and continuity project. The first job is to get user data safely out while the original service still exists. The next job is to make that preserved data useful through local viewers, portable databases, cloud migration, compatible APIs, and future clients.

## Token Use Boundary

The Simply Plural API token is used only for exporting a user's own data from Simply Plural / Apparyllis while that service is still available.

The Simply Plural token is not a PluralBridge account credential.

The Simply Plural token will not be used for ongoing PluralBridge development, future PluralBridge services, future PluralBridge viewers, cloud access, mobile apps, REST services, or authentication after export.

PluralBridge will create and maintain its own authentication platform for any future hosted services, cloud-backed tools, REST APIs, or multi-device access.

PluralBridge is an independent preservation and migration toolkit maintained by **Needs of the Many** (`@needsofmany`).

It helps users export, preserve, and bridge Simply Plural data into local JSON files, SQL databases, reports, viewers, converters, importers, and future tools.

## Independent Project

PluralBridge is an independent community preservation effort.

PluralBridge is not affiliated with Simply Plural, Apparyllis, or the Simply Plural development team.

The project uses public Apparyllis REST API endpoints with a user-created API token. It does not require reverse engineering, decompiling, disassembling, patching, intercepting, or modifying Simply Plural software.

## Current Status

PluralBridge is in an early preservation-focused stage.

The current repository contains documentation, safe examples, Python starter scripts, SQL Server script skeletons, and safety checks. The first priority is preserving user data locally in a way that keeps private data private.

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
