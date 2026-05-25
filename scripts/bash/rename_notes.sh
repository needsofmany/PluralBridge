#!/usr/bin/env bash
set -euo pipefail
set +H

NOTES_DIR="${1:-exports/notes}"

if [ ! -d "$NOTES_DIR" ]; then
  echo "Notes directory not found: $NOTES_DIR"
  exit 1
fi

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
