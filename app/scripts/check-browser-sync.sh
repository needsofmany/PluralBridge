#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

SRC_ROOT="app/src"
SERVED_ROOT="api/PluralBridge.Api/PluralBridge.Api/wwwroot/app"

if [[ ! -d "$SRC_ROOT" ]]; then
  echo "SYNC_CHECK_MISSING_SRC_ROOT"
  exit 1
fi

if [[ ! -d "$SERVED_ROOT" ]]; then
  echo "SYNC_CHECK_MISSING_SERVED_ROOT"
  exit 1
fi

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

src_logical="$tmp_dir/src.logical.txt"
served_logical="$tmp_dir/served.logical.txt"
only_src="$tmp_dir/only.src.txt"
only_served="$tmp_dir/only.served.txt"
shared="$tmp_dir/shared.txt"
content_drift="$tmp_dir/content.drift.txt"

# Source logical mapping:
# - app/src/js/app.js  -> app.js
# - app/src/css/app.css -> app.css
# - everything else remains relative path
rg --files "$SRC_ROOT" \
  | sed 's#\\#/#g' \
  | sed "s#^$SRC_ROOT/##" \
  | sed 's#^js/app\.js$#app.js#' \
  | sed 's#^css/app\.css$#app.css#' \
  | sort -u > "$src_logical"

# Served logical mapping is already in final shape
rg --files "$SERVED_ROOT" \
  | sed 's#\\#/#g' \
  | rg -v '^api/PluralBridge\.Api/PluralBridge\.Api/wwwroot/app/DO_NOT_EDIT_GENERATED\.md$' \
  | sed "s#^$SERVED_ROOT/##" \
  | rg -v '^DO_NOT_EDIT_GENERATED\.md$' \
  | sort -u > "$served_logical"

comm -23 "$src_logical" "$served_logical" > "$only_src"
comm -13 "$src_logical" "$served_logical" > "$only_served"
comm -12 "$src_logical" "$served_logical" > "$shared"

: > "$content_drift"
while IFS= read -r rel; do
  [[ -z "$rel" ]] && continue

  if [[ "$rel" == "app.js" ]]; then
    src_file="$SRC_ROOT/js/app.js"
    served_file="$SERVED_ROOT/app.js"
  elif [[ "$rel" == "app.css" ]]; then
    src_file="$SRC_ROOT/css/app.css"
    served_file="$SERVED_ROOT/app.css"
  else
    src_file="$SRC_ROOT/$rel"
    served_file="$SERVED_ROOT/$rel"
  fi

  if ! cmp -s "$src_file" "$served_file"; then
    echo "$rel" >> "$content_drift"
  fi
done < "$shared"

only_src_count="$(wc -l < "$only_src" | tr -d ' ')"
only_served_count="$(wc -l < "$only_served" | tr -d ' ')"
content_drift_count="$(wc -l < "$content_drift" | tr -d ' ')"

echo "SYNC_CHECK_ONLY_IN_SRC=$only_src_count"
echo "SYNC_CHECK_ONLY_IN_SERVED=$only_served_count"
echo "SYNC_CHECK_CONTENT_DRIFT=$content_drift_count"

if [[ "$only_src_count" != "0" || "$only_served_count" != "0" || "$content_drift_count" != "0" ]]; then
  echo "SYNC_CHECK_FAILED"
  exit 1
fi

echo "SYNC_CHECK_OK"
