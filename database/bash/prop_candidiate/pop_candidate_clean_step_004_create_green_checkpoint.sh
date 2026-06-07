#!/usr/bin/env bash
cd "/d/Src/Needs of the Many/PluralBridge"
out="database/targeting/prop_candidate/pop_candidate_clean_step_004_green_checkpoint.md"
printf "%s\n" \
"# PluralBridge 1-6 Green Checkpoint" \
"" \
"Validated after failed schema-generation path was archived." \
"" \
"## Fresh target folder" \
"" \
"database/targeting/prop_candidate/" \
"" \
"## Failed target archive" \
"" \
"database/targeting/prop_candidate_fail_20260606_112536" \
"" \
"## Failed bash archive" \
"" \
"database/bash/prop_candidiate_fail_20260606_112900" \
"" \
"## Proven green SSMS run order" \
"" \
"1. Select/highlight: master" \
"   Run: database/targeting/prop_candidate/pop_candidate_step_002_create_repeatbuild_database.sql" \
"" \
"2. Select/highlight: PluralBridge_RepeatBuild" \
"   Run: database/targeting/prop_candidate/pop_candidate_step_007_seed_source_systems_literal.sql" \
"" \
"3. Select/highlight: PluralBridge_RepeatBuild" \
"   Run: database/targeting/prop_candidate/pop_candidate_step_014_003_populate_from_prequel_001_006_candidate_firstpass_fixed.sql" \
"" \
"4. Select/highlight: PluralBridge_RepeatBuild" \
"   Run: database/targeting/prop_candidate/pop_candidate_step_006_validate_001_006_slice.sql" \
"" \
"## Validated counts" \
"" \
"| CheckName | ExpectedCount | ActualCount | Status |" \
"|---|---:|---:|---|" \
"| pb_custom_fields | 7 | 7 | PASS |" \
"| pb_front_history | 886 | 886 | PASS |" \
"| pb_import_batches | 1 | 1 | PASS |" \
"| pb_members | 49 | 49 | PASS |" \
"| pb_privacy_buckets | 2 | 2 | PASS |" \
"| pb_source_id_map 1-6 total | 945 | 945 | PASS |" \
"| pb_source_records 1-6 total | 945 | 945 | PASS |" \
"| pb_systems | 1 | 1 | PASS |" \
"" \
"## Rule" \
"" \
"Everything after the archived schema-generation fork is disposable. Resume only from this green checkpoint." \
> "$out"
echo "VERIFY START"
ls -l "$out"
grep -n "Proven green SSMS run order" "$out"
grep -n "pb_front_history" "$out"
grep -n "disposable" "$out"
echo "VERIFY END"
