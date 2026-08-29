#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

PROGRAM_CS="api/PluralBridge.Api/PluralBridge.Api/Program.cs"
SRC_JS_DIR="app/src/js"
SRC_CSS_DIR="app/src/css"

if [[ ! -f "$PROGRAM_CS" ]]; then
  echo "ALLOWLIST_CHECK_MISSING_PROGRAM_CS"
  exit 1
fi

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

expected_js="$tmp_dir/expected-js.txt"
expected_css="$tmp_dir/expected-css.txt"
actual_js="$tmp_dir/actual-js.txt"
actual_css="$tmp_dir/actual-css.txt"

# expected JS/CSS from app/src (excluding top-level app.js/app.css)
rg --files "$SRC_JS_DIR" \
  | sed 's#\\#/#g' \
  | sed 's#^.*/##' \
  | rg '\.js$' \
  | rg -v '^app\.js$' \
  | sort -u > "$expected_js"

rg --files "$SRC_CSS_DIR" \
  | sed 's#\\#/#g' \
  | sed 's#^.*/##' \
  | rg '\.css$' \
  | rg -v '^app\.css$' \
  | sort -u > "$expected_css"

# actual allowlist entries parsed from Program.cs
awk '
  /allowedBrowserJsFiles/ { in_js=1; next }
  in_js && /\}/ { in_js=0 }
  in_js {
    if (match($0, /"[^"]+"/)) {
      s=substr($0, RSTART+1, RLENGTH-2); print s
    }
  }
' "$PROGRAM_CS" | sort -u > "$actual_js"

awk '
  /allowedBrowserCssFiles/ { in_css=1; next }
  in_css && /\}/ { in_css=0 }
  in_css {
    if (match($0, /"[^"]+"/)) {
      s=substr($0, RSTART+1, RLENGTH-2); print s
    }
  }
' "$PROGRAM_CS" | sort -u > "$actual_css"

missing_js="$(comm -23 "$expected_js" "$actual_js" || true)"
extra_js="$(comm -13 "$expected_js" "$actual_js" || true)"
missing_css="$(comm -23 "$expected_css" "$actual_css" || true)"
extra_css="$(comm -13 "$expected_css" "$actual_css" || true)"

js_missing_count="$(printf "%s\n" "$missing_js" | awk 'NF{c++} END{print c+0}')"
js_extra_count="$(printf "%s\n" "$extra_js" | awk 'NF{c++} END{print c+0}')"
css_missing_count="$(printf "%s\n" "$missing_css" | awk 'NF{c++} END{print c+0}')"
css_extra_count="$(printf "%s\n" "$extra_css" | awk 'NF{c++} END{print c+0}')"

echo "ALLOWLIST_JS_MISSING=$js_missing_count"
echo "ALLOWLIST_JS_EXTRA=$js_extra_count"
echo "ALLOWLIST_CSS_MISSING=$css_missing_count"
echo "ALLOWLIST_CSS_EXTRA=$css_extra_count"

if [[ "$js_missing_count" != "0" || "$js_extra_count" != "0" || "$css_missing_count" != "0" || "$css_extra_count" != "0" ]]; then
  [[ "$js_missing_count" != "0" ]] && echo "ALLOWLIST_JS_MISSING_ITEMS:" && printf "%s\n" "$missing_js"
  [[ "$js_extra_count" != "0" ]] && echo "ALLOWLIST_JS_EXTRA_ITEMS:" && printf "%s\n" "$extra_js"
  [[ "$css_missing_count" != "0" ]] && echo "ALLOWLIST_CSS_MISSING_ITEMS:" && printf "%s\n" "$missing_css"
  [[ "$css_extra_count" != "0" ]] && echo "ALLOWLIST_CSS_EXTRA_ITEMS:" && printf "%s\n" "$extra_css"
  echo "ALLOWLIST_CHECK_FAILED"
  exit 1
fi

echo "ALLOWLIST_CHECK_OK"
