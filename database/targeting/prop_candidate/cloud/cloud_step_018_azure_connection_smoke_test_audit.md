# Cloud Step 18 Azure Connection Smoke Test Audit

Date: 2026-06-06 PT

## Checkpoint

- Branch: feature/002-cloud-readiness
- Local HEAD: 8a38e90
- SQL file: database/targeting/prop_candidate/cloud/cloud_step_018_azure_connection_smoke_test.sql
- Execution surface: SSMS against Azure SQL

## Azure SQL target

- Server endpoint: pluralbridge-cloudproof-syf001.database.windows.net
- ServerName result: pluralbridge-cloudproof-syf001
- Database: PluralBridgeCloudProof001
- LoginName result: pbadmin
- EngineEdition result: 5
- Edition result: SQL Azure
- ProductVersion result: 12.0.2000.8

## Database verification

- CurrentDatabase result: PluralBridgeCloudProof001
- Compatibility level: 170
- Collation: SQL_Latin1_General_CP1_CI_AS
- State: ONLINE
- Containment: NONE
- dbo schema present: yes
- Existing PluralBridge pb_* tables: none

## Result

Cloud Step 18 passed. SSMS can connect to the empty Azure SQL proof database, and the target database contains no existing PluralBridge pb_* tables.

## Next action

Generate or identify the Azure-compatible schema source for the pb_* 1-6 proof tables before any DDL/DML execution.
