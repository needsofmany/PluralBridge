# Cloud Step 20A Azure Schema Candidate Audit

Date: 2026-06-06 PT

## Checkpoint

- Branch: feature/002-cloud-readiness
- Local HEAD: 1fba552
- Azure write check before this step: READ_WRITE, CanCreateTable = 1, CanAlterDatabase = 1

## Decision

Schema inventory found the 1-6 pb_* table/constraint shape in an archived failed candidate folder.

The archived SQL file is not executed directly. A fresh Azure-target candidate was generated under database/targeting/prop_candidate/cloud/.

## Files

- Schema reference inspected: database/targeting/prop_candidate_fail_20260606_112536/pop_candidate_step_021_create_001_006_schema_idempotent.sql
- Generated Azure schema candidate: database/targeting/prop_candidate/cloud/cloud_step_020_create_azure_schema_001_006.sql

## Boundary

No SQL was executed by Git Bash. Azure SQL DDL execution remains SSMS-only.
