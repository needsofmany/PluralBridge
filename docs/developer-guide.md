# Developer Guide

This guide is for developers and technical users working with PluralBridge.

PluralBridge is an independent preservation and migration toolkit. It uses public Apparyllis REST API endpoints and a user-created API token to export Simply Plural data into local files.

## Project Boundaries

PluralBridge uses public API access.

It does not require:

- reverse engineering Simply Plural website code
- reverse engineering Simply Plural mobile app code
- decompiling
- disassembling
- patching
- intercepting private traffic
- modifying Simply Plural software
- bypassing authentication

## Local Development Layout

- scripts/bash/        Bash helper scripts
- scripts/python/      Python export helpers
- scripts/sqlserver/   SQL Server import scripts
- docs/                Documentation
- examples/            Safe example configuration files
- reports/             Report notes and examples
- tests/               Future tests

## Token Handling

Use the `SP_TOKEN` environment variable. Do not hard-code tokens in scripts, examples, tests, documentation, or commit history.

## Data Handling

Do not commit real exported data.

Avoid committing:

- JSON exports
- notes
- avatar images
- SQL Server database backups
- generated reports containing private data
- screenshots containing private data

Use synthetic or heavily redacted examples.
