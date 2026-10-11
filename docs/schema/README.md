# Schema Docs Overview

This folder contains public schema references for PluralBridge import and recovery work.

## How to use this folder

- Treat these files as observed source-shape references, not product requirements.
- Prefer official Simply Plural export shape first.
- Use Samsung Smart Switch `simply.db` docs as a recovery/discovery fallback.
- Do not commit personal backup data, tokens, signed URLs, or member-identifying samples.

## File-by-file guide

| File | Purpose |
|---|---|
| `simply-plural-export-format.md` | Human-readable reference for official Simply Plural export JSON shape. |
| `simply_plural_last_export.inferred.schema.json` | Machine-readable inferred JSON schema generated from `simply-plural-export-format.md`. |
| `simply-plural-export-format-updated-from-pluralkit.md` | Extended/updated observed export-shape notes for known gaps. |
| `samsung-smart-switch-simply-db-format.md` | Human-readable reference for observed JSON recovered from Samsung Smart Switch `simply.db`. |
| `samsung_smart_switch_simply_db.inferred.schema.json` | Machine-readable inferred JSON schema generated from `samsung-smart-switch-simply-db-format.md`. |
| `samsung-smart-switch-backup-layout.md` | Generalized Smart Switch backup folder/layout reference for recovery planning. |
| `samsung-smart-switch-photo-info-format.md` | Observed `PHOTO_INFO` shape reference for media metadata correlation. |
| `samsung-smart-switch-sp-recovery.md` | Generalized recovery workflow notes linking backup artifacts to import planning. |
| `pluralbridge-db-schema-format.md` | Human-readable reference for current PluralBridge working SQL schema. |
| `script.sql` | SQL schema script snapshot used as provenance for `pluralbridge-db-schema-format.md`. |
| `PluralBridge-DB-Schema.png` | Visual ERD-style diagram of the working database schema. |

## Suggested read order for outside developers

1. `simply-plural-export-format.md`
2. `simply_plural_last_export.inferred.schema.json`
3. `pluralbridge-db-schema-format.md`
4. `script.sql`
5. Recovery-specific `samsung-*` docs only when working fallback recovery/import paths

## Provenance note

Generated files in this folder are derived from checked-in markdown schema references so external contributors can validate assumptions without private repo access.
