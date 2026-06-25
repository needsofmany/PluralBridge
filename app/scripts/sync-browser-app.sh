#!/usr/bin/env bash
set -u

SRC_APP="app/src"
SERVED_APP="api/PluralBridge.Api/PluralBridge.Api/wwwroot/app"

if [[ ! -d "$SRC_APP" ]]; then
  printf "SYNC_BROWSER_APP_MISSING_SRC_APP\n"
  exit 1
fi

if [[ ! -d "$SERVED_APP" ]]; then
  printf "SYNC_BROWSER_APP_MISSING_SERVED_APP\n"
  exit 1
fi

for required_file in "$SRC_APP/index.html" "$SRC_APP/js/app.js" "$SRC_APP/js/legacy-app.js" "$SRC_APP/css/app.css" "$SRC_APP/css/legacy-app.css"; do
  if [[ ! -f "$required_file" ]]; then
    printf "SYNC_BROWSER_APP_MISSING_REQUIRED_FILE=%s\n" "$required_file"
    exit 1
  fi
done

mkdir -p "$SERVED_APP/js" || {
  printf "SYNC_BROWSER_APP_CREATE_SERVED_JS_FAILED\n"
  exit 1
}

mkdir -p "$SERVED_APP/css" || {
  printf "SYNC_BROWSER_APP_CREATE_SERVED_CSS_FAILED\n"
  exit 1
}

cp -p "$SRC_APP/index.html" "$SERVED_APP/index.html" || {
  printf "SYNC_BROWSER_APP_COPY_INDEX_FAILED\n"
  exit 1
}

cp -p "$SRC_APP/js/app.js" "$SERVED_APP/app.js" || {
  printf "SYNC_BROWSER_APP_COPY_APP_JS_FAILED\n"
  exit 1
}

cp -p "$SRC_APP/js/"*.js "$SERVED_APP/js/" || {
  printf "SYNC_BROWSER_APP_COPY_JS_MODULES_FAILED\n"
  exit 1
}

cp -p "$SRC_APP/css/app.css" "$SERVED_APP/app.css" || {
  printf "SYNC_BROWSER_APP_COPY_APP_CSS_FAILED\n"
  exit 1
}

cp -p "$SRC_APP/css/"*.css "$SERVED_APP/css/" || {
  printf "SYNC_BROWSER_APP_COPY_CSS_MODULES_FAILED\n"
  exit 1
}

if [[ -d "$SRC_APP/assets" ]]; then
  mkdir -p "$SERVED_APP/assets" || {
    printf "SYNC_BROWSER_APP_CREATE_SERVED_ASSETS_FAILED\n"
    exit 1
  }
  cp -R "$SRC_APP/assets/." "$SERVED_APP/assets/" || {
    printf "SYNC_BROWSER_APP_COPY_ASSETS_FAILED\n"
    exit 1
  }
fi

printf "SYNC_BROWSER_APP_INDEX_COPIED=1\n"
printf "SYNC_BROWSER_APP_JS_COPIED=1\n"
printf "SYNC_BROWSER_APP_CSS_COPIED=1\n"
printf "SYNC_BROWSER_APP_OK\n"
