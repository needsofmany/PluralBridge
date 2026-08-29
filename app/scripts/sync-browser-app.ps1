$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$srcApp = "app/src"
$servedApp = "api/PluralBridge.Api/PluralBridge.Api/wwwroot/app"

if (!(Test-Path $srcApp -PathType Container)) {
  Write-Output "SYNC_BROWSER_APP_MISSING_SRC_APP"
  exit 1
}

if (!(Test-Path $servedApp -PathType Container)) {
  Write-Output "SYNC_BROWSER_APP_MISSING_SERVED_APP"
  exit 1
}

$required = @(
  "$srcApp/index.html",
  "$srcApp/js/app.js",
  "$srcApp/js/legacy-app.js",
  "$srcApp/css/app.css",
  "$srcApp/css/legacy-app.css"
)

foreach ($f in $required) {
  if (!(Test-Path $f -PathType Leaf)) {
    Write-Output "SYNC_BROWSER_APP_MISSING_REQUIRED_FILE=$f"
    exit 1
  }
}

New-Item -ItemType Directory -Force -Path "$servedApp/js" | Out-Null
New-Item -ItemType Directory -Force -Path "$servedApp/css" | Out-Null

Remove-Item "$servedApp/js/app.js" -Force -ErrorAction SilentlyContinue
Remove-Item "$servedApp/css/app.css" -Force -ErrorAction SilentlyContinue

Copy-Item "$srcApp/index.html" "$servedApp/index.html" -Force
Copy-Item "$srcApp/js/app.js" "$servedApp/app.js" -Force
Get-ChildItem "$srcApp/js/*.js" |
  Where-Object { $_.Name -ne "app.js" } |
  Copy-Item -Destination "$servedApp/js/" -Force
Copy-Item "$srcApp/css/app.css" "$servedApp/app.css" -Force
Get-ChildItem "$srcApp/css/*.css" |
  Where-Object { $_.Name -ne "app.css" } |
  Copy-Item -Destination "$servedApp/css/" -Force

if (Test-Path "$srcApp/assets" -PathType Container) {
  New-Item -ItemType Directory -Force -Path "$servedApp/assets" | Out-Null
  Copy-Item "$srcApp/assets/*" "$servedApp/assets/" -Recurse -Force
}

Write-Output "SYNC_BROWSER_APP_INDEX_COPIED=1"
Write-Output "SYNC_BROWSER_APP_JS_COPIED=1"
Write-Output "SYNC_BROWSER_APP_CSS_COPIED=1"
Write-Output "SYNC_BROWSER_APP_OK"