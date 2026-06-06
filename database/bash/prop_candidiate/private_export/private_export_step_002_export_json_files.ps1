param(
    [string]$ServerInstance = "YELLOW-FISH-PC\YELLOWFISH",
    [string]$Database = "PluralBridge_RepeatBuild"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName "System.Data"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ScriptRoot "..\..\..\..")).Path
$OutputDir = Join-Path $RepoRoot "database\targeting\prop_candidate\private_export\json"
$AuditPath = Join-Path $OutputDir "_audit_private_export_001_006.txt"
$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$Tables = @(
    [pscustomobject]@{
        Name = "pb_source_systems"
        Expected = 1
    },
    [pscustomobject]@{
        Name = "pb_import_batches"
        Expected = 1
    },
    [pscustomobject]@{
        Name = "pb_systems"
        Expected = 1
    },
    [pscustomobject]@{
        Name = "pb_members"
        Expected = 49
    },
    [pscustomobject]@{
        Name = "pb_privacy_buckets"
        Expected = 2
    },
    [pscustomobject]@{
        Name = "pb_custom_fields"
        Expected = 7
    },
    [pscustomobject]@{
        Name = "pb_front_history"
        Expected = 886
    },
    [pscustomobject]@{
        Name = "pb_source_records"
        Expected = 945
    },
    [pscustomobject]@{
        Name = "pb_source_id_map"
        Expected = 945
    }
)

function Quote-SqlIdentifier {
    param([string]$Name)
    return "[" + ($Name -replace "]", "]]") + "]"
}

function Invoke-Scalar {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql
    )
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 120
    try {
        return $cmd.ExecuteScalar()
    }
    finally {
        $cmd.Dispose()
    }
}

function Invoke-JsonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Sql
    )
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $Sql
    $cmd.CommandTimeout = 300
    $reader = $cmd.ExecuteReader()
    $sb = New-Object System.Text.StringBuilder
    try {
        while ($reader.Read()) {
            if (-not $reader.IsDBNull(0)) {
                [void]$sb.Append($reader.GetString(0))
            }
        }
    }
    finally {
        $reader.Close()
        $cmd.Dispose()
    }
    $json = $sb.ToString()
    if ([string]::IsNullOrWhiteSpace($json)) {
        return "[]"
    }
    return $json
}

function Get-PrimaryKeyOrderBy {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$TableName
    )
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = @"
SELECT c.name
FROM sys.key_constraints kc
JOIN sys.tables t ON t.object_id = kc.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.index_columns ic ON ic.object_id = t.object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
WHERE kc.type = @ConstraintType
AND s.name = @SchemaName
AND t.name = @TableName
ORDER BY ic.key_ordinal
"@
    $cmd.Parameters.Add("@ConstraintType", [System.Data.SqlDbType]::NVarChar, 2) | Out-Null
    $cmd.Parameters["@ConstraintType"].Value = "PK"
    $cmd.Parameters.Add("@SchemaName", [System.Data.SqlDbType]::NVarChar, 128) | Out-Null
    $cmd.Parameters["@SchemaName"].Value = "dbo"
    $cmd.Parameters.Add("@TableName", [System.Data.SqlDbType]::NVarChar, 128) | Out-Null
    $cmd.Parameters["@TableName"].Value = $TableName
    $reader = $cmd.ExecuteReader()
    $columns = New-Object System.Collections.Generic.List[string]
    try {
        while ($reader.Read()) {
            $columns.Add($reader.GetString(0))
        }
    }
    finally {
        $reader.Close()
        $cmd.Dispose()
    }
    if ($columns.Count -eq 0) {
        throw "No primary key found for dbo.$TableName"
    }
    $quoted = @()
    foreach ($column in $columns) {
        $quoted += Quote-SqlIdentifier $column
    }
    return ($quoted -join ", ")
}

function Get-JsonArrayCount {
    param([string]$JsonText)
    $trimmed = $JsonText.Trim()
    if ($trimmed.Length -eq 0) {
        return 0
    }
    if ($trimmed -eq "[]") {
        return 0
    }
    $parsed = $trimmed | ConvertFrom-Json
    return @($parsed).Count
}

$ConnectionString = "Server=$ServerInstance;Database=$Database;Integrated Security=True;TrustServerCertificate=True;ApplicationIntent=ReadOnly;"
$Connection = New-Object System.Data.SqlClient.SqlConnection
$Connection.ConnectionString = $ConnectionString

$AuditLines = New-Object System.Collections.Generic.List[string]
$GeneratedUtc = [DateTime]::UtcNow.ToString("o")
$GeneratedLocal = [DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss K")
$AuditLines.Add("PRIVATE EXPORT 001-006")
$AuditLines.Add("GeneratedUtc=$GeneratedUtc")
$AuditLines.Add("GeneratedLocal=$GeneratedLocal")
$AuditLines.Add("ServerInstance=$ServerInstance")
$AuditLines.Add("Database=$Database")
$AuditLines.Add("ApplicationIntent=ReadOnly")
$AuditLines.Add("OutputDir=$OutputDir")

try {
    $Connection.Open()
    foreach ($table in $Tables) {
        $tableName = $table.Name
        $quotedTable = Quote-SqlIdentifier $tableName
        $orderBy = Get-PrimaryKeyOrderBy -Connection $Connection -TableName $tableName
        $countSql = "SELECT COUNT_BIG(*) FROM dbo.$quotedTable"
        $jsonSql = "SELECT * FROM dbo.$quotedTable ORDER BY $orderBy FOR JSON PATH, INCLUDE_NULL_VALUES"
        $count = [int64](Invoke-Scalar -Connection $Connection -Sql $countSql)
        $json = Invoke-JsonQuery -Connection $Connection -Sql $jsonSql
        $jsonCount = [int64](Get-JsonArrayCount -JsonText $json)
        if ($count -ne [int64]$table.Expected) {
            throw "$tableName SQL count mismatch. Expected $($table.Expected), got $count"
        }
        if ($jsonCount -ne [int64]$table.Expected) {
            throw "$tableName JSON count mismatch. Expected $($table.Expected), got $jsonCount"
        }
        $fileName = "$tableName.json"
        $filePath = Join-Path $OutputDir $fileName
        [System.IO.File]::WriteAllText($filePath, $json, $Utf8NoBom)
        $hash = (Get-FileHash -Algorithm SHA256 -Path $filePath).Hash
        $payload = "$tableName|count=$count|json_count=$jsonCount|sha256=$hash|file=$fileName"
        $AuditLines.Add($payload)
        Write-Host $payload
    }
}
finally {
    if ($Connection.State -ne [System.Data.ConnectionState]::Closed) {
        $Connection.Close()
    }
    $Connection.Dispose()
}

[System.IO.File]::WriteAllLines($AuditPath, $AuditLines, $Utf8NoBom)
$AuditHash = (Get-FileHash -Algorithm SHA256 -Path $AuditPath).Hash
Write-Host "_audit_private_export_001_006.txt|sha256=$AuditHash"

$ExpectedFiles = @(
    "pb_source_systems.json",
    "pb_import_batches.json",
    "pb_systems.json",
    "pb_members.json",
    "pb_privacy_buckets.json",
    "pb_custom_fields.json",
    "pb_front_history.json",
    "pb_source_records.json",
    "pb_source_id_map.json",
    "_audit_private_export_001_006.txt"
)
$missing = @()
foreach ($expectedFile in $ExpectedFiles) {
    $fullPath = Join-Path $OutputDir $expectedFile
    if (-not (Test-Path $fullPath)) {
        $missing += $expectedFile
    }
}
if ($missing.Count -gt 0) {
    throw "Missing expected export files: $($missing -join ", ")"
}

Write-Host "PRIVATE_JSON_EXPORT_PASS"
