$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptRoot "..\..\..\..")).Path
$JsonDir = Join-Path $RepoRoot "database\targeting\prop_candidate\private_export\json"
$AuditPath = Join-Path $JsonDir "_audit_private_export_001_006.txt"

$Expected = @{
    pb_source_systems = 1
    pb_import_batches = 1
    pb_systems = 1
    pb_members = 49
    pb_privacy_buckets = 2
    pb_custom_fields = 7
    pb_front_history = 886
    pb_source_records = 945
    pb_source_id_map = 945
}

if (-not (Test-Path $JsonDir)) {
    throw "JSON export directory missing: $JsonDir"
}
if (-not (Test-Path $AuditPath)) {
    throw "Audit file missing: $AuditPath"
}

Write-Host "PRIVATE_JSON_DISK_TEST_START"
Write-Host "JsonDir=$JsonDir"

$AuditText = Get-Content -Raw -Path $AuditPath
$Names = @(
    "pb_source_systems",
    "pb_import_batches",
    "pb_systems",
    "pb_members",
    "pb_privacy_buckets",
    "pb_custom_fields",
    "pb_front_history",
    "pb_source_records",
    "pb_source_id_map"
)

foreach ($name in $Names) {
    $path = Join-Path $JsonDir "$name.json"
    if (-not (Test-Path $path)) {
        throw "$name missing JSON file"
    }
    $json = Get-Content -Raw -Path $path
    $parsed = $json | ConvertFrom-Json
    $count = @($parsed).Count
    $expectedCount = [int]$Expected[$name]
    if ($count -ne $expectedCount) {
        throw "$name count mismatch. Expected $expectedCount, got $count"
    }
    $hash = (Get-FileHash -Algorithm SHA256 -Path $path).Hash
    $auditPattern = [regex]::Escape("$name|count=$expectedCount|json_count=$expectedCount|sha256=$hash|file=$name.json")
    if ($AuditText -notmatch $auditPattern) {
        throw "$name audit hash/count line mismatch"
    }
    Write-Host "$name DISK_JSON_PARSE_PASS count=$count sha256=$hash"
}

$auditHash = (Get-FileHash -Algorithm SHA256 -Path $AuditPath).Hash
Write-Host "_audit_private_export_001_006.txt DISK_HASH_PASS sha256=$auditHash"
Write-Host "PRIVATE_JSON_DISK_TEST_PASS"
Write-Host "PRIVATE_JSON_DISK_TEST_END"
