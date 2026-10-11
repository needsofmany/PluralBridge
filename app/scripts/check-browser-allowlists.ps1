$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$programCs = "api/PluralBridge.Api/PluralBridge.Api/Program.cs"
$srcJsDir = "app/src/js"
$srcCssDir = "app/src/css"

if (!(Test-Path $programCs -PathType Leaf)) {
  Write-Output "ALLOWLIST_CHECK_MISSING_PROGRAM_CS"
  exit 1
}

function Get-AllowlistEntries([string[]]$lines, [string]$marker) {
  $entries = [System.Collections.Generic.List[string]]::new()
  $inBlock = $false

  foreach ($line in $lines) {
    if (-not $inBlock -and $line -match [Regex]::Escape($marker)) {
      $inBlock = $true
      continue
    }

    if ($inBlock -and $line -match "^\s*};\s*$") {
      break
    }

    if ($inBlock) {
      $matches = [regex]::Matches($line, '"([^"]+)"')
      foreach ($match in $matches) {
        $entries.Add($match.Groups[1].Value)
      }
    }
  }

  return $entries | Sort-Object -Unique
}

$expectedJs = @()
if (Test-Path $srcJsDir -PathType Container) {
  $expectedJs = Get-ChildItem -Path $srcJsDir -File -Filter *.js |
    Where-Object { $_.Name -ine "app.js" } |
    Select-Object -ExpandProperty Name |
    Sort-Object -Unique
}

$expectedCss = @()
if (Test-Path $srcCssDir -PathType Container) {
  $expectedCss = Get-ChildItem -Path $srcCssDir -File -Filter *.css |
    Where-Object { $_.Name -ine "app.css" } |
    Select-Object -ExpandProperty Name |
    Sort-Object -Unique
}

$programLines = Get-Content -Path $programCs
$actualCss = Get-AllowlistEntries $programLines "allowedBrowserCssFiles"
$actualJs = Get-AllowlistEntries $programLines "allowedBrowserJsFiles"

$expectedJsSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$expectedCssSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$actualJsSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$actualCssSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

$expectedJs | ForEach-Object { [void]$expectedJsSet.Add($_) }
$expectedCss | ForEach-Object { [void]$expectedCssSet.Add($_) }
$actualJs | ForEach-Object { [void]$actualJsSet.Add($_) }
$actualCss | ForEach-Object { [void]$actualCssSet.Add($_) }

$missingJs = $expectedJs | Where-Object { -not $actualJsSet.Contains($_) }
$extraJs = $actualJs | Where-Object { -not $expectedJsSet.Contains($_) }
$missingCss = $expectedCss | Where-Object { -not $actualCssSet.Contains($_) }
$extraCss = $actualCss | Where-Object { -not $expectedCssSet.Contains($_) }

$jsMissingCount = ($missingJs | Measure-Object).Count
$jsExtraCount = ($extraJs | Measure-Object).Count
$cssMissingCount = ($missingCss | Measure-Object).Count
$cssExtraCount = ($extraCss | Measure-Object).Count

Write-Output "ALLOWLIST_JS_MISSING=$jsMissingCount"
Write-Output "ALLOWLIST_JS_EXTRA=$jsExtraCount"
Write-Output "ALLOWLIST_CSS_MISSING=$cssMissingCount"
Write-Output "ALLOWLIST_CSS_EXTRA=$cssExtraCount"

if ($jsMissingCount -ne 0 -or $jsExtraCount -ne 0 -or $cssMissingCount -ne 0 -or $cssExtraCount -ne 0) {
  if ($jsMissingCount -ne 0) {
    Write-Output "ALLOWLIST_JS_MISSING_ITEMS:"
    $missingJs | ForEach-Object { Write-Output $_ }
  }

  if ($jsExtraCount -ne 0) {
    Write-Output "ALLOWLIST_JS_EXTRA_ITEMS:"
    $extraJs | ForEach-Object { Write-Output $_ }
  }

  if ($cssMissingCount -ne 0) {
    Write-Output "ALLOWLIST_CSS_MISSING_ITEMS:"
    $missingCss | ForEach-Object { Write-Output $_ }
  }

  if ($cssExtraCount -ne 0) {
    Write-Output "ALLOWLIST_CSS_EXTRA_ITEMS:"
    $extraCss | ForEach-Object { Write-Output $_ }
  }

  Write-Output "ALLOWLIST_CHECK_FAILED"
  exit 1
}

Write-Output "ALLOWLIST_CHECK_OK"
