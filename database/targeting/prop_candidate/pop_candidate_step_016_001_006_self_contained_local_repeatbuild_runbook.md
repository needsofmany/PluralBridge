# PluralBridge 1-6 self-contained local repeat-build runbook

## Status

Validated on 2026-06-06.

This run order replaces the baseline-copy source-system seed with a literal repository-controlled APPARYLLIS seed.

## SSMS run order

1. Select/highlight database: master
   Run: database/targeting/prop_candidate/pop_candidate_step_002_create_repeatbuild_database.sql

2. Select/highlight database: PluralBridge_RepeatBuild
   Run: database/targeting/prop_candidate/pop_candidate_step_007_seed_source_systems_literal.sql

3. Select/highlight database: PluralBridge_RepeatBuild
   Run: database/targeting/prop_candidate/pop_candidate_step_014_003_populate_from_prequel_001_006_candidate_firstpass_fixed.sql

4. Select/highlight database: PluralBridge_RepeatBuild
   Run: database/targeting/prop_candidate/pop_candidate_step_006_validate_001_006_slice.sql

## Successful validation counts

| CheckName | ExpectedCount | ActualCount | Status |
|---|---:|---:|---|
| pb_custom_fields | 7 | 7 | PASS |
| pb_front_history | 886 | 886 | PASS |
| pb_import_batches | 1 | 1 | PASS |
| pb_members | 49 | 49 | PASS |
| pb_privacy_buckets | 2 | 2 | PASS |
| pb_source_id_map 1-6 total | 945 | 945 | PASS |
| pb_source_records 1-6 total | 945 | 945 | PASS |
| pb_systems | 1 | 1 | PASS |

## Remaining local dependencies

1. Schema still comes from DBCC CLONEDATABASE against PluralBridge.
2. Population data still comes from PluralBridge-Prequel.
