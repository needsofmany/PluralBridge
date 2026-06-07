# Private Export Step 4 Green Checkpoint

Date: 2026-06-06 PT

## Status

Private Export Step 2 and Step 3 are green.

The validated export source is PluralBridge_RepeatBuild on SQL Server instance YELLOW-FISH-PC\YELLOWFISH.

The export path uses PowerShell SqlClient with ApplicationIntent=ReadOnly. SQL data extraction is limited to SELECT queries using FOR JSON PATH, INCLUDE_NULL_VALUES. No sqlcmd, SSMS result-grid copying, SSMS wizard, INSERT text generation, or write/DDL SQL path is part of this checkpoint.

## Generated script artifacts

- database/bash/prop_candidiate/private_export/private_export_step_002_export_json_files.ps1
- database/bash/prop_candidiate/private_export/private_export_step_003_verify_json_export.sh

## Generated private JSON artifacts

- database/targeting/prop_candidate/private_export/json/pb_source_systems.json
- database/targeting/prop_candidate/private_export/json/pb_import_batches.json
- database/targeting/prop_candidate/private_export/json/pb_systems.json
- database/targeting/prop_candidate/private_export/json/pb_members.json
- database/targeting/prop_candidate/private_export/json/pb_privacy_buckets.json
- database/targeting/prop_candidate/private_export/json/pb_custom_fields.json
- database/targeting/prop_candidate/private_export/json/pb_front_history.json
- database/targeting/prop_candidate/private_export/json/pb_source_records.json
- database/targeting/prop_candidate/private_export/json/pb_source_id_map.json
- database/targeting/prop_candidate/private_export/json/_audit_private_export_001_006.txt

## Validated counts

| Table | Expected count | JSON count | Status |
|---|---:|---:|---|
| pb_source_systems | 1 | 1 | PASS |
| pb_import_batches | 1 | 1 | PASS |
| pb_systems | 1 | 1 | PASS |
| pb_members | 49 | 49 | PASS |
| pb_privacy_buckets | 2 | 2 | PASS |
| pb_custom_fields | 7 | 7 | PASS |
| pb_front_history | 886 | 886 | PASS |
| pb_source_records | 945 | 945 | PASS |
| pb_source_id_map | 945 | 945 | PASS |

## Step 3 external audit result

- NO_WRITE_OR_DDL_TOKENS_PASS
- pb_source_systems HASH_PASS
- pb_import_batches HASH_PASS
- pb_systems HASH_PASS
- pb_members HASH_PASS
- pb_privacy_buckets HASH_PASS
- pb_custom_fields HASH_PASS
- pb_front_history HASH_PASS
- pb_source_records HASH_PASS
- pb_source_id_map HASH_PASS
- Audit file SHA256: 659FCDED0E1688062DAB8A8117E46E254D248DD9A463659C8F2FA83982A182F6
- VERIFY PRIVATE EXPORT PASS

## Green boundary

This checkpoint establishes a validated private JSON export for the 1-6 local repeat-build slice. The files under database/targeting/prop_candidate/private_export/json/ contain private data and are not for publication.

Next safe step: verify git status and decide whether to commit only the generator/audit/checkpoint artifacts, while keeping private JSON output ignored or otherwise out of public history.
