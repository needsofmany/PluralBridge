# Notes Export

PluralBridge can preserve Simply Plural notes as local JSON files.

Notes may contain some of the most private material in a Simply Plural account. Treat exported notes as sensitive personal data.

## Recommended Output Folder

Recommended local folder:

```text
exports/notes/
```

This folder should stay out of Git.

The repository `.gitignore` should exclude:

```text
exports/
Notes/
```

## Privacy Warning

Notes may contain:

- private journal-style text
- member-specific information
- system history
- relationship information
- safety information
- medical or therapeutic context
- names or identifying details
- timestamps
- internal IDs

Do not commit real exported notes to Git unless you intentionally chose to make them public.

Before committing, run:

```bash
git status
git diff --cached --stat
git diff --cached
```

Review staged files carefully before pushing.

## Filename Privacy

Avoid note filenames that expose private names, decorator names, titles, or labels.

Recommended neutral filenames:

```text
1.json
2.json
3.json
```

Avoid filenames based on:

- note titles
- member names
- decorator names
- fronting labels
- private categories
- relationship labels

Neutral filenames reduce accidental exposure when folders are viewed, copied, backed up, or screenshotted.

## Rename Existing Note Files

If note files were exported with decorated filenames, rename them to neutral numeric filenames.

This script renames all `.json` files in a notes folder to `1.json`, `2.json`, `3.json`, and so on.

Save as:

```text
scripts/bash/rename_notes.sh
```

Script:

```bash
#!/usr/bin/env bash
set -euo pipefail
set +H

NOTES_DIR="${1:-exports/notes}"

cd "$NOTES_DIR"

if ! ls -1 *.json >/dev/null 2>&1; then
  echo "No .json files found in: $NOTES_DIR"
  exit 1
fi

echo "Renaming JSON note files in:"
echo "$NOTES_DIR"
echo

n=1
for f in $(ls -1 *.json | sort); do
  tmp="$(printf "__tmp_note_%05d.json" "$n")"
  mv -- "$f" "$tmp"
  n=$((n + 1))
done

n=1
for f in $(ls -1 __tmp_note_*.json | sort); do
  new="$(printf "%d.json" "$n")"
  mv -- "$f" "$new"
  echo "$f -> $new"
  n=$((n + 1))
done

echo
echo "Done. Renamed $((n - 1)) files."
```

## Run the Rename Script

From the repository root:

```bash
chmod +x scripts/bash/rename_notes.sh
./scripts/bash/rename_notes.sh exports/notes
```

If your notes folder is somewhere else, pass that folder path:

```bash
./scripts/bash/rename_notes.sh "exports/notes"
```

The script uses `set +H` because Bash treats `!` as history expansion unless that behavior is disabled.

## Validate JSON Files

After export or rename, validate the notes with Python:

```bash
python - <<'PY'
import json
from pathlib import Path

root = Path("exports/notes")

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

## Count Notes

Count note files:

```bash
find exports/notes -maxdepth 1 -type f -name "*.json" | wc -l
```

List the first few filenames:

```bash
find exports/notes -maxdepth 1 -type f -name "*.json" | sort | head
```

Expected neutral shape:

```text
exports/notes/1.json
exports/notes/2.json
exports/notes/3.json
```

## Redacted Examples

Use synthetic or redacted note examples in documentation and tests.

Good example:

```json
{
  "id": "note_id_redacted",
  "content": {
    "title": "Redacted Note Title",
    "text": "Redacted note body."
  }
}
```

Avoid examples containing real note text, real names, real IDs, or real timestamps unless intentionally published.

## SQL Server Use

Notes may later be loaded into SQL Server tables.

Keep the raw note JSON files as the first preservation layer. SQL import scripts should treat notes as sensitive and should avoid embedding real note content in scripts, examples, screenshots, or documentation.
