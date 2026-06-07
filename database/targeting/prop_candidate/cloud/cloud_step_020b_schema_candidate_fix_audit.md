# Cloud Step 20B Schema Candidate Fix Audit

Date: 2026-06-06 PT

## Checkpoint

- Branch: feature/002-cloud-readiness
- Local HEAD: 1fba552

## Failure

- Failed SQL file: cloud_step_020_create_azure_schema_001_006.sql
- SSMS error: Msg 102, incorrect syntax near Target.
- Cause: Bash generation stripped quotes from the THROW message text.
- SQL failed before BEGIN TRANSACTION; expected Azure target state remains unchanged.

## Fix

- Failed SQL archived under database/targeting/prop_candidate_fail_20260606_cloud_schema_throw_quote_error/.
- Replacement SQL: database/targeting/prop_candidate/cloud/cloud_step_020b_create_azure_schema_001_006.sql
- DDL execution remains SSMS-only.
