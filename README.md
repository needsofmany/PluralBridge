# PluralBridge

[![GitGem](https://gitgem.org/api/badge/github/needsofmany/PluralBridge.svg)](https://gitgem.org/gem/github/needsofmany/PluralBridge)

Project website: https://thepluralbridge.org/

## Start Here

Simply Plural shut down on July 1, 2026.

If you already exported your Simply Plural data, keep the export private, backed up, and somewhere you control:

- https://thepluralbridge.org/start-here
- https://thepluralbridge.org/simply-plural-shutdown
- https://thepluralbridge.org/browser-app

If you missed the export window, PluralBridge cannot promise recovery of data that was never exported. Do not send account credentials, API tokens, screenshots, or private System data through email, public threads, issues, or direct messages.

Downloading the GitHub ZIP gives you the PluralBridge source code and project files. It does not provide a finished double-click installer.

## What PluralBridge Is

PluralBridge is an independent, open-source preservation, import, and continuity project for plural Systems affected by the Simply Plural shutdown.

The project is building a privacy-centered browser application that will let users:

1. Create a PluralBridge account and profile.
2. Review a preserved Simply Plural export before anything is saved.
3. Import preserved data through a deliberate, auditable process.
4. View, add, and update member profiles.
5. Keep control over who can see and change System data.

PluralBridge is maintained by **Needs of the Many** (`@needsofmany`).

## Current Status

PluralBridge is working toward its first browser-based release.

Current development focuses on:

- Account registration, verification, login, recovery, and profile management.
- Authorization, ownership, consent, and audit boundaries.
- Import preview and validation for preserved Simply Plural exports.
- Browser-based member viewing and editing.
- Privacy-sensitive storage and migration architecture.
- Safe, portable data structures for future clients and compatible tools.

The browser release is intended to run on Windows, macOS, Linux, phones, tablets, and other devices with a modern browser.

## Existing Simply Plural Exports

A Simply Plural export may contain deeply private information, including member names, notes, descriptions, pronouns, custom fields, fronting history, privacy settings, friends data, avatar images, timestamps, and internal identifiers.

Protect preserved exports as private records:

- Keep at least one backup.
- Store files somewhere under your control.
- Avoid uploading exports to public services or repositories.
- Do not attach real exports to GitHub issues or discussions.
- Use synthetic or heavily redacted data when reporting a problem.

## Independent Project

PluralBridge is not affiliated with Simply Plural, Apparyllis, or the Simply Plural development team.

PluralBridge treats Simply Plural as the source of user-owned preserved data, not as a product-design template. PluralBridge clients, services, authentication, interfaces, privacy controls, and storage architecture are independently designed.

The legacy PluralBridge exporter used public Apparyllis REST API endpoints with a user-created API token. It did not require reverse engineering, decompiling, disassembling, patching, intercepting, or modifying Simply Plural software.

A Simply Plural API token is not a PluralBridge account credential and has no role in PluralBridge authentication.

## Legacy Export Tooling

The repository retains earlier Python export scripts, SQL scripts, examples, and documentation for historical reference and for understanding exports that users preserved before shutdown.

Simply Plural's servers are no longer available. The legacy scripts cannot create a new export, and users should not spend time obtaining an API token or troubleshooting API connectivity.

Legacy export components include:

    scripts/python/export_json.py
    scripts/python/export_avatars.py
    scripts/python/pluralbridge/
    scripts/sqlserver/
    examples/redacted-json-shapes/

These materials may still help developers understand the preserved data format and build import, conversion, validation, or reporting tools.

## Repository Layout

    api/                  PluralBridge API and service code
    app/src/              Canonical browser application source (edit here)
    app/                  Legacy browser mirror/scripts (runtime files migrating; avoid editing)
    database/             Database schema and migration work
    docs/                 Project and developer documentation
    examples/             Synthetic and redacted examples
    reports/              Report notes and examples
    scripts/bash/         Repository and development helpers
    scripts/python/       Legacy export and data-processing helpers
    scripts/sqlserver/    Legacy import and reporting scripts
    tests/                Automated tests
    tools/                Supporting project tools
    website/              Public website deployed through Cloudflare Pages

## Developer Start

Clone the repository:

    git clone https://github.com/needsofmany/PluralBridge.git
    cd PluralBridge

Use the developer documentation and task-specific project notes for current build, database, and test instructions.

The repository is under active development. Review the current branch, open issues, pull requests, and documentation before assuming that older setup instructions remain current.

## Privacy and Safety

Do not commit or publish:

- Simply Plural API tokens.
- PluralBridge credentials.
- Real JSON exports.
- Member names or notes.
- Avatar images.
- SQL Server database backups.
- Generated reports containing private data.
- Screenshots containing private System data.
- Machine-specific private paths.

Use synthetic or heavily redacted examples.

## Repository Safety Check

Before committing or publishing changes, run:

    ./scripts/bash/check_repo_safety.sh

This checks tracked files for likely token leakage, private identity strings, private local paths, exported-data folders, avatar images, note files, database files, and old SQL Server database-name references.

Before committing:

1. Run the repository safety check.
2. Review `git status`.
3. Review staged file names.
4. Confirm no real exported data is staged.
5. Confirm no token, credential, or private path is staged.

Suggested checks:

    ./scripts/bash/check_repo_safety.sh
    git status
    git diff --cached --stat
    git diff --cached --name-only

## Documentation

Project documentation is stored under:

    docs/

Some export, token, installation, and regular-user documents were written before the July 1, 2026 shutdown. Treat those files as historical material unless they explicitly state that they have been updated for the post-shutdown project.

## Public Website

The public PluralBridge website is available at:

- https://thepluralbridge.org
- https://www.thepluralbridge.org
- https://pluralpedia.org/w/PluralBridge
- https://github.com/needsofmany/PluralBridge/discussions

The website source is stored in the `website/` directory and is deployed through Cloudflare Pages from the `master` branch, with `website` as the build output directory.

GitHub Discussions is used for project planning, contributor questions, design proposals, feature ideas, documentation suggestions, and structured polls:

- https://github.com/needsofmany/PluralBridge/discussions

## Import Path for Other Tools

PluralBridge is intended to support the broader plural-tooling ecosystem.

Other projects may use PluralBridge as an import bridge for preserved Simply Plural data. The current goal is to make preserved data safer to review, import, convert, validate, and continue using across independently designed tools.

## SQL Server

Legacy SQL Server scripts live under:

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

SQL scripts must remain data-agnostic. They must not contain real exported data, member names, notes, API tokens, credentials, private paths, or machine-specific folders.

## Examples

Safe examples live under:

    examples/

Redacted JSON-shape examples live under:

    examples/redacted-json-shapes/

These examples are synthetic or redacted. Do not replace them with real exported data.

## Reports

Report notes and examples live under:

    reports/

Reports should use synthetic examples or schema-level descriptions unless a user intentionally generates a private local report for their own use.

## Database Schema Diagram

A SQL Server schema diagram is available at:

    docs/images/PluralBridge-DB-Schema.png

The diagram shows the tested preservation schema, including members, front history, notes, avatars, custom fields, privacy buckets, friends, and chat tables.

## License

PluralBridge is licensed under the GNU General Public License v3.0.

Copyright (C) 2026 Needs of the Many
