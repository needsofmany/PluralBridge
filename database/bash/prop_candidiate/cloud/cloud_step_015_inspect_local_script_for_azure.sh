#!/usr/bin/env bash
set -e
echo "CLOUD STEP 15 INSPECTION START"
src="database/targeting/prop_candidate/pop_candidate_one_script_001_local_repeatbuild_001_006.sql"
out="database/targeting/prop_candidate/cloud/cloud_step_015_local_script_azure_inspection.txt"
tmpdir="database/targeting/prop_candidate/cloud/cloud_step_015_tmp"
rm -rf "$tmpdir"
mkdir -p "$tmpdir"
rm -f "$out"
if [ ! -f "$src" ]
then
    echo "SOURCE_MISSING=$src"
    exit 1
fi
echo "SOURCE=$src" | tee -a "$out"
echo "SOURCE_SHA256=$(sha256sum "$src" | awk '{print toupper($1)}')" | tee -a "$out"
echo "SOURCE_LINES=$(wc -l < "$src" | tr -d ' ')" | tee -a "$out"
echo "" | tee -a "$out"
report_pattern()
{
    label="$1"
    pattern="$2"
    safe_label="$(printf "%s" "$label" | tr " /" "__" | tr -cd "A-Za-z0-9_")"
    hitfile="$tmpdir/$safe_label.txt"
    if grep -nE "$pattern" "$src" > "$hitfile"
    then
        count="$(wc -l < "$hitfile" | tr -d ' ')"
    else
        count="0"
    fi
    echo "CHECK=$label COUNT=$count" | tee -a "$out"
    if [ "$count" != "0" ]
    then
        sed -n "1,20p" "$hitfile" | tee -a "$out"
    fi
    echo "" | tee -a "$out"
}
report_pattern "Dead database reference" "PluralBridge_RebuildScratch"
report_pattern "Local repeatbuild database reference" "PluralBridge_RepeatBuild"
report_pattern "Prequel reference" "PluralBridge-Prequel"
report_pattern "Preop reference" "PluralBridge_Preop"
report_pattern "USE statement" "^[[:space:]]*USE[[:space:]]+"
report_pattern "CREATE DATABASE statement" "CREATE[[:space:]]+DATABASE"
report_pattern "DROP DATABASE statement" "DROP[[:space:]]+DATABASE"
report_pattern "ALTER DATABASE statement" "ALTER[[:space:]]+DATABASE"
report_pattern "File path or filegroup option" "FILENAME|FILEGROUP|TEXTIMAGE_ON|ON[[:space:]]+PRIMARY"
report_pattern "Single user multi user rollback option" "SINGLE_USER|MULTI_USER|ROLLBACK[[:space:]]+IMMEDIATE"
report_pattern "SQLCMD directive" "^[[:space:]]*:"
report_pattern "SQLCMD variable token" "\\$\\([A-Za-z0-9_]+\\)"
report_pattern "Bulk external file operation" "BULK[[:space:]]+INSERT|OPENROWSET|xp_cmdshell|sp_configure"
report_pattern "Private JSON path leak" "private_export/json|\\.json"
report_pattern "GO batch separator" "^[[:space:]]*GO[[:space:]]*$"
rm -rf "$tmpdir"
echo "AUDIT_FILE=$out"
echo "CLOUD_STEP_015_INSPECTION_COMPLETE"
echo "CLOUD STEP 15 INSPECTION END"
