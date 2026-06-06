$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptRoot "..\..\..\..")).Path
$JsonDir = Join-Path $RepoRoot "database\targeting\prop_candidate\private_export\json"

$Tables = @(
    [pscustomobject]@{ Name = "pb_source_systems"; Expected = 1; Unique = @("SourceSystemId") },
    [pscustomobject]@{ Name = "pb_import_batches"; Expected = 1; Unique = @("ImportBatchId") },
    [pscustomobject]@{ Name = "pb_systems"; Expected = 1; Unique = @("SystemId") },
    [pscustomobject]@{ Name = "pb_members"; Expected = 49; Unique = @("MemberId") },
    [pscustomobject]@{ Name = "pb_privacy_buckets"; Expected = 2; Unique = @("PrivacyBucketId") },
    [pscustomobject]@{ Name = "pb_custom_fields"; Expected = 7; Unique = @("CustomFieldId") },
    [pscustomobject]@{ Name = "pb_front_history"; Expected = 886; Unique = @("FrontHistoryId") },
    [pscustomobject]@{ Name = "pb_source_records"; Expected = 945; Unique = @("SourceRecordId") },
    [pscustomobject]@{ Name = "pb_source_id_map"; Expected = 945; Unique = @("SourceIdMapId") }
)

function Read-JsonArray {
    param([string]$Name)
    $path = Join-Path $JsonDir "$Name.json"
    if (-not (Test-Path $path)) { throw "$Name missing JSON file: $path" }
    $json = Get-Content -Raw -Path $path
    return @($json | ConvertFrom-Json)
}

function Get-PropertyNames {
    param([object[]]$Rows)
    $set = New-Object System.Collections.Generic.SortedSet[string]
    foreach ($row in $Rows) {
        foreach ($prop in $row.PSObject.Properties.Name) {
            [void]$set.Add($prop)
        }
    }
    return @($set)
}

function Test-UniqueColumn {
    param(
        [string]$TableName,
        [object[]]$Rows,
        [string]$ColumnName
    )
    if ($Rows.Count -eq 0) { return }
    if (-not ($Rows[0].PSObject.Properties.Name -contains $ColumnName)) {
        Write-Host "$TableName.$ColumnName UNIQUE_SKIPPED column_not_present"
        return
    }
    $values = @($Rows | ForEach-Object { $_.$ColumnName })
    $missing = @($values | Where-Object { $null -eq $_ -or "$_".Length -eq 0 }).Count
    $unique = @($values | Sort-Object -Unique).Count
    if ($missing -ne 0) { throw "$TableName.$ColumnName missing values: $missing" }
    if ($unique -ne $Rows.Count) { throw "$TableName.$ColumnName duplicate values. Rows=$($Rows.Count), Unique=$unique" }
    Write-Host "$TableName.$ColumnName UNIQUE_PASS rows=$($Rows.Count)"
}

Write-Host "PRIVATE_JSON_STRUCTURE_AUDIT_START"
Write-Host "JsonDir=$JsonDir"

foreach ($table in $Tables) {
    $rows = Read-JsonArray -Name $table.Name
    if ($rows.Count -ne $table.Expected) {
        throw "$($table.Name) count mismatch. Expected=$($table.Expected), Actual=$($rows.Count)"
    }
    $props = Get-PropertyNames -Rows $rows
    Write-Host "$($table.Name) COUNT_PASS count=$($rows.Count) property_count=$($props.Count)"
    Write-Host "$($table.Name) PROPERTIES $($props -join ",")"
    foreach ($column in $table.Unique) {
        Test-UniqueColumn -TableName $table.Name -Rows $rows -ColumnName $column
    }
}

Write-Host "PRIVATE_JSON_STRUCTURE_AUDIT_PASS"
Write-Host "PRIVATE_JSON_STRUCTURE_AUDIT_END"
