# Cloud Step 23 Azure private data load audit

- Step: 22E / 23 audit
- Branch: feature/002-cloud-readiness
- Head before audit: 85728c0
- Azure database: PluralBridgeCloudProof001
- Executed private SQL file: database/targeting/prop_candidate/private_export/json/cloud_step_022e_load_azure_private_data_001_006.sql
- Private SQL status: ignored, not for commit
- Result: PASS

## Count results

| Table | Actual | Expected | Result |
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

## Next action

Checkpoint the Azure 1-6 schema-plus-data proof, excluding ignored private JSON and private generated SQL.
