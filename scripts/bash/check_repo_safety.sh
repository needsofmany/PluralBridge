#!/usr/bin/env bash
set -euo pipefail
set +H

echo "PluralBridge repository safety check"
echo

echo "1. Checking tracked files for token, identity, and private path leakage..."
if git grep -nE "TBD///|Authorization: [A-Za-z0-9]|dd\.dice|Sharky|Yellowfish|stacy|gmail|hotmail|D:[/\\]+![/\\]+|/d/!/" -- . ":(exclude)scripts/bash/check_repo_safety.sh"; then
  echo
  echo "FAILED: potential private/token/path leakage found."
  exit 1
else
  echo "OK: no tracked token, identity, or private path matches found."
fi

echo
echo "2. Checking for tracked exported data, images, notes, or database files..."
if git ls-files \
  | grep -Ev "^(docs/images/PluralBridge-DB-Schema\.png|website/images/background\.jpg)$" \
  | grep -Ei "exports/|member_images|notes/|\.bak$|\.mdf$|\.ldf$|\.sqlite|\.db$|\.png$|\.jpg$|\.jpeg$|\.gif$|\.webp$"; then
  echo
  echo "FAILED: potentially private exported/generated files are tracked."
  exit 1
else
  echo "OK: no tracked export folders, avatar images, note files, or database files found."
fi

echo
echo "3. Checking for old SQL Server database name..."
if git grep -nE "CREATE DATABASE SimplyPlural|USE SimplyPlural|DB_ID\\(N'SimplyPlural'\\)" -- . ":(exclude)scripts/bash/check_repo_safety.sh"; then
  echo
  echo "FAILED: old SQL Server database name found."
  exit 1
else
  echo "OK: old SQL Server database name not found."
fi

echo
echo "Safety check passed."
