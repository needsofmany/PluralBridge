#!/usr/bin/env bash
set -euo pipefail

SCRIPT="database/bash/prop_candidiate/private_export/private_export_step_002_export_json_files.ps1"
DIR="database/targeting/prop_candidate/private_export/json"
AUDIT="$DIR/_audit_private_export_001_006.txt"

echo "VERIFY PRIVATE EXPORT START"
test -f "$SCRIPT"
test -f "$AUDIT"

grep -n "ApplicationIntent=ReadOnly" "$SCRIPT"
grep -n "FOR JSON PATH, INCLUDE_NULL_VALUES" "$SCRIPT"
grep -n "PRIVATE_JSON_EXPORT_PASS" "$SCRIPT"

if grep -nE "\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|TRUNCATE)\b" "$SCRIPT"
then
    echo "UNEXPECTED_WRITE_OR_DDL_TOKEN_FAIL"
    exit 1
else
    echo "NO_WRITE_OR_DDL_TOKENS_PASS"
fi

grep -nE "^pb_source_systems\|count=1\|json_count=1\|sha256=[0-9A-F]{64}\|file=pb_source_systems\.json$" "$AUDIT"
grep -nE "^pb_import_batches\|count=1\|json_count=1\|sha256=[0-9A-F]{64}\|file=pb_import_batches\.json$" "$AUDIT"
grep -nE "^pb_systems\|count=1\|json_count=1\|sha256=[0-9A-F]{64}\|file=pb_systems\.json$" "$AUDIT"
grep -nE "^pb_members\|count=49\|json_count=49\|sha256=[0-9A-F]{64}\|file=pb_members\.json$" "$AUDIT"
grep -nE "^pb_privacy_buckets\|count=2\|json_count=2\|sha256=[0-9A-F]{64}\|file=pb_privacy_buckets\.json$" "$AUDIT"
grep -nE "^pb_custom_fields\|count=7\|json_count=7\|sha256=[0-9A-F]{64}\|file=pb_custom_fields\.json$" "$AUDIT"
grep -nE "^pb_front_history\|count=886\|json_count=886\|sha256=[0-9A-F]{64}\|file=pb_front_history\.json$" "$AUDIT"
grep -nE "^pb_source_records\|count=945\|json_count=945\|sha256=[0-9A-F]{64}\|file=pb_source_records\.json$" "$AUDIT"
grep -nE "^pb_source_id_map\|count=945\|json_count=945\|sha256=[0-9A-F]{64}\|file=pb_source_id_map\.json$" "$AUDIT"

for name in pb_source_systems pb_import_batches pb_systems pb_members pb_privacy_buckets pb_custom_fields pb_front_history pb_source_records pb_source_id_map
do
    file="$DIR/$name.json"
    test -s "$file"
    audit_hash="$(grep "^$name|count=" "$AUDIT" | awk -F"sha256=" "{print \$2}" | awk -F"|" "{print \$1}")"
    actual_hash="$(sha256sum "$file" | awk "{print toupper(\$1)}")"
    if [ "$audit_hash" != "$actual_hash" ]
    then
        echo "$name HASH_FAIL"
        echo "audit=$audit_hash"
        echo "actual=$actual_hash"
        exit 1
    fi
    echo "$name HASH_PASS"
done

sha256sum "$AUDIT" | awk "{print toupper(\$1) \"  _audit_private_export_001_006.txt\"}"
echo "VERIFY PRIVATE EXPORT PASS"
echo "VERIFY PRIVATE EXPORT END"
