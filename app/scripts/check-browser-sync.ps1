$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$srcRoot = "app/src"
$servedRoot = "api/PluralBridge.Api/PluralBridge.Api/wwwroot/app"

if (!(Test-Path $srcRoot -PathType Container)) {
  Write-Output "SYNC_CHECK_MISSING_SRC_ROOT"
  exit 1
}

if (!(Test-Path $servedRoot -PathType Container)) {
  Write-Output "SYNC_CHECK_MISSING_SERVED_ROOT"
  exit 1
}

function Get-RelativePath([string]$rootPath, [string]$fullPath) {
  $rootUri = [System.Uri]((Resolve-Path $rootPath).Path.TrimEnd('\') + '\')
  $fileUri = [System.Uri]((Resolve-Path $fullPath).Path)
  $relativeUri = $rootUri.MakeRelativeUri($fileUri)
  return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('\', '/')
}

function Map-SourceLogical([string]$relativePath) {
  if ($relativePath -ieq "js/app.js") { return "app.js" }
  if ($relativePath -ieq "css/app.css") { return "app.css" }
  return $relativePath
}

$srcLogical = [System.Collections.Generic.List[string]]::new()
Get-ChildItem -Path $srcRoot -Recurse -File | ForEach-Object {
  $relative = Get-RelativePath $srcRoot $_.FullName
  $srcLogical.Add((Map-SourceLogical $relative))
}

$servedLogical = [System.Collections.Generic.List[string]]::new()
Get-ChildItem -Path $servedRoot -Recurse -File | ForEach-Object {
  $relative = Get-RelativePath $servedRoot $_.FullName
  if ($relative -ieq "DO_NOT_EDIT_GENERATED.md") {
    return
  }
  $servedLogical.Add($relative)
}

$srcLogicalSorted = $srcLogical | Sort-Object -Unique
$servedLogicalSorted = $servedLogical | Sort-Object -Unique

$srcSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$servedSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

$srcLogicalSorted | ForEach-Object { [void]$srcSet.Add($_) }
$servedLogicalSorted | ForEach-Object { [void]$servedSet.Add($_) }

$onlySrc = $srcLogicalSorted | Where-Object { -not $servedSet.Contains($_) }
$onlyServed = $servedLogicalSorted | Where-Object { -not $srcSet.Contains($_) }
$shared = $srcLogicalSorted | Where-Object { $servedSet.Contains($_) }

$contentDrift = [System.Collections.Generic.List[string]]::new()
foreach ($rel in $shared) {
  if ([string]::IsNullOrWhiteSpace($rel)) {
    continue
  }

  if ($rel -ieq "app.js") {
    $srcFile = Join-Path $srcRoot "js/app.js"
    $servedFile = Join-Path $servedRoot "app.js"
  }
  elseif ($rel -ieq "app.css") {
    $srcFile = Join-Path $srcRoot "css/app.css"
    $servedFile = Join-Path $servedRoot "app.css"
  }
  else {
    $srcFile = Join-Path $srcRoot $rel
    $servedFile = Join-Path $servedRoot $rel
  }

  if (!(Test-Path $srcFile -PathType Leaf) -or !(Test-Path $servedFile -PathType Leaf)) {
    $contentDrift.Add($rel)
    continue
  }

  $srcHash = (Get-FileHash -Path $srcFile -Algorithm SHA256).Hash
  $servedHash = (Get-FileHash -Path $servedFile -Algorithm SHA256).Hash

  if ($srcHash -ne $servedHash) {
    $contentDrift.Add($rel)
  }
}

$onlySrcCount = ($onlySrc | Measure-Object).Count
$onlyServedCount = ($onlyServed | Measure-Object).Count
$contentDriftCount = ($contentDrift | Measure-Object).Count

Write-Output "SYNC_CHECK_ONLY_IN_SRC=$onlySrcCount"
Write-Output "SYNC_CHECK_ONLY_IN_SERVED=$onlyServedCount"
Write-Output "SYNC_CHECK_CONTENT_DRIFT=$contentDriftCount"

if ($onlySrcCount -ne 0 -or $onlyServedCount -ne 0 -or $contentDriftCount -ne 0) {
  Write-Output "SYNC_CHECK_FAILED"
  exit 1
}

Write-Output "SYNC_CHECK_OK"
