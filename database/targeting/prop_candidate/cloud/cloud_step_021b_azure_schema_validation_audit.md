# Cloud Step 21B Azure Schema Validation Audit

Date: 2026-06-06 PT

## Checkpoint

- Branch: feature/002-cloud-readiness
- Local HEAD: 1fba552
- Execution surface: SSMS against Azure SQL
- Server: pluralbridge-cloudproof-syf001.database.windows.net
- Database: PluralBridgeCloudProof001
- Validation SQL: database/targeting/prop_candidate/cloud/cloud_step_021_validate_azure_schema_001_006.sql

## Validation result

- Database check: PASS
- pb_* table count: 9 PASS
- Missing expected tables: 0 PASS
- Missing expected core columns: 0 PASS
- Primary key count: 9 PASS
- Foreign key count: 10 PASS
- Unique constraint count: 1 PASS
- Missing expected constraints: 0 PASS

## Column counts

- pb_custom_fields: 10
- pb_front_history: 12
- pb_import_batches: 10
- pb_members: 17
- pb_privacy_buckets: 10
- pb_source_id_map: 8
- pb_source_records: 9
- pb_source_systems: 5
- pb_systems: 11

## Result

Cloud Step 21 passed. Azure SQL now has the expected empty 1-6 PluralBridge schema with validated tables, columns, primary keys, foreign keys, and unique constraint.

## Next action

Choose the cloud data-load path. The safest next proof path is to generate an Azure JSON import script from the ignored private JSON files, execute only in SSMS, then validate counts against the known 1-6 totals.
