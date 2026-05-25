# JSON Export

PluralBridge preserves Simply Plural data by saving API responses as local JSON files.

JSON export is the first preservation layer. It keeps the API response data in a portable local format before any SQL import, report generation, conversion, or viewer work happens.

## Requirements

You need:

- a Simply Plural / Apparyllis API token
- Git Bash, PowerShell, or another shell capable of running `curl`
- a local output folder
- enough disk space for exported JSON and avatar files

For token setup, see:

```text
docs/api-token.md
```

## Recommended Output Folder

Recommended local folder:

```text
exports/json/
```

This folder should stay out of Git.

The repository `.gitignore` should exclude:

```text
exports/
spdump/
```

## API Base

PluralBridge examples use this API base:

```text
https://api.apparyllis.com
```

In Git Bash:

```bash
export SP_API="https://api.apparyllis.com"
```

## Token Setup

Set your token in Git Bash:

```bash
read -s -p "Paste Simply Plural token: " SP_TOKEN
echo
export SP_TOKEN
```

Check that the variable has a value without printing the token:

```bash
printf 'Token length: %s\n' "${#SP_TOKEN}"
```

## Basic Authentication Test

Run:

```bash
curl -i \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/me"
```

A working token should return account data.

A missing or invalid token may return:

```text
HTTP/1.1 401 Unauthorized
Authorization token is missing or invalid.
```

## Create Export Folder

From the repository root:

```bash
mkdir -p exports/json
```

## Export Account Data

Save the authenticated user/account response:

```bash
curl -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/me" \
  -o exports/json/me.json
```

## Extract User ID Without jq

If `jq` is not installed, Python can read the user ID from `me.json`.

```bash
export SP_UID="$(
python - <<'PY'
import json

with open("exports/json/me.json", "r", encoding="utf-8") as f:
    data = json.load(f)

value = (
    data.get("id")
    or data.get("uid")
    or data.get("content", {}).get("id")
    or data.get("content", {}).get("uid")
)

print(value or "")
PY
)"

printf 'SP_UID=%s\n' "$SP_UID"
```

If `SP_UID` is blank, inspect `exports/json/me.json` and confirm the token test succeeded.

## Export Members

```bash
curl -sS \
  -H "Authorization: ${SP_TOKEN}" \
  "${SP_API}/v1/members/${SP_UID}" \
  -o exports/json/members.json
```

## Export Additional Endpoints

The exact endpoint list may change as PluralBridge grows.

Examples may include:

```text
categories
channels
customFields
customFronts
filters
friends
frontHistory
groups
privacyBuckets
polls
timers/automated
timers/repeated
```

Each export should write one JSON file.

Recommended filename style:

```text
categories.json
channels.json
customFields.json
frontHistory.json
members.json
privacyBuckets.json
```

## Empty Responses

Some endpoints may return empty JSON responses.

An empty array may appear as:

```json
[]
```

A file containing only an empty array may be two bytes.

Keep these files during preservation. An empty response still records that the endpoint was checked.

## File Naming

Use neutral filenames.

Recommended:

```text
members.json
frontHistory.json
privacyBuckets.json
```

Avoid filenames containing private member names, decorator names, note titles, or system labels.

## Validation

After export, list the files:

```bash
find exports/json -maxdepth 1 -type f -name "*.json" -print
```

Check file sizes:

```bash
ls -lh exports/json
```

Optionally validate JSON with Python:

```bash
python - <<'PY'
import json
from pathlib import Path

root = Path("exports/json")

failed = False

for path in sorted(root.glob("*.json")):
    try:
        with path.open("r", encoding="utf-8") as f:
            json.load(f)
        print(f"OK   {path}")
    except Exception as exc:
        failed = True
        print(f"FAIL {path}: {exc}")

raise SystemExit(1 if failed else 0)
PY
```

## Privacy Warning

Exported JSON may contain private data.

Do not commit exported JSON files unless they are redacted examples created intentionally for documentation or tests.

Before committing, run:

```bash
git status
git diff --cached --stat
git diff --cached
```

## Next Steps

After JSON export, users may choose to:

- download avatar images
- export notes
- load JSON into SQL Server
- create local reports
- build viewers or converters

## Current Tested Export Coverage

The current Python exporter has been tested against a live Simply Plural account and writes the main export files under:

    exports/json

The tested JSON export set includes:

    categories.json
    channels.json
    customFields.json
    customFronts.json
    filters.json
    friends.json
    frontHistory.json
    fronthistory_starttime_and_endtime.json
    groups.json
    manifest.json
    me.json
    members.json
    polls.json
    privacyBuckets.json
    timers__automated.json
    timers__repeated.json
    user.json

The exporter also writes per-member notes under:

    exports/notes

Notes use neutral numeric filenames such as:

    1.json
    2.json
    3.json

The mapping from each neutral note filename back to its member API endpoint is recorded in:

    exports/json/manifest.json

This allows SQL Server import scripts and future tools to join note files back to member IDs without exposing member names in filenames.

## Empty JSON Files

Some exported files may contain an empty JSON array.

For example:

    []

This is still useful preservation information. An empty file means the endpoint was queried successfully and returned no rows for that data type at the time of export.

## Re-running Exports

The export process is safe to run multiple times while the Simply Plural service remains available.

This is important for preservation. Users should run an export first, even if they plan to clean up, rename, edit, or reorganize Simply Plural data later.

After making changes in Simply Plural, run the export again to create a newer snapshot.

For dated snapshots, use separate output folders rather than overwriting the previous export.

Example:

    python scripts/python/export_json.py --output-dir exports/json-2026-05-08 --notes-dir exports/notes-2026-05-08

A later export can use another folder:

    python scripts/python/export_json.py --output-dir exports/json-2026-05-09 --notes-dir exports/notes-2026-05-09

The safest preservation workflow is:

1. Export now.
2. Back up the export.
3. Make changes in Simply Plural.
4. Export again.
5. Keep the latest snapshot, and optionally keep older snapshots for comparison.
