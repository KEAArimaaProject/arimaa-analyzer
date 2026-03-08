# Create and seed the regular MySQL database from Sql_arimaa_* files (not the MysqlTests)
#
# Usage (from project root or anywhere):
#   powershell -ExecutionPolicy Bypass -File .\run_mysql_database.ps1
#   # or, in an existing PS session:
#   .\run_mysql_database.ps1
#
# Optional: skip data dump import
#   .\run_mysql_database.ps1 -SkipDataDump

param(
  [switch]$SkipDataDump,
  [int]$WaitTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'

# Ensure we execute from the script's directory (expected to be the repo root)
Set-Location -LiteralPath $PSScriptRoot

Write-Host "[1/4] Starting MySQL with docker compose..." -ForegroundColor Cyan
docker compose -f .\Documents\Database\docker-compose.yml up -d

Write-Host "[2/4] Waiting for MySQL inside the container to be ready (timeout: ${WaitTimeoutSeconds}s)..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds($WaitTimeoutSeconds)
$ready = $false
while ((Get-Date) -lt $deadline) {
  docker exec -e MYSQL_PWD=123456 arimaadockermysqldb sh -lc 'mysqladmin ping -h 127.0.0.1 -uroot --silent' | Out-Null
  if ($LASTEXITCODE -eq 0) { $ready = $true; break }
  Start-Sleep -Seconds 1
}
if (-not $ready) {
  Write-Warning "MySQL did not become ready within $WaitTimeoutSeconds seconds. Showing last 100 container log lines:"
  docker logs --tail 100 arimaadockermysqldb 2>$null
  throw "MySQL readiness timeout."
}

Write-Host "[3/4] Building schema and routines from Sql_arimaa_* files..." -ForegroundColor Cyan
$env:MYSQL_PWD = '123456'

# Build the database from the core scripts in Documents/Database/mysql
# Use single-quoted here-string so MySQL backticks aren't interpreted by PowerShell (e.g., `a → bell)
$ddl = @'
DROP SCHEMA IF EXISTS `arimaadockermysqldb`;
CREATE SCHEMA `arimaadockermysqldb` DEFAULT CHARACTER SET utf8;
USE `arimaadockermysqldb`;

SOURCE Documents/Database/mysql/Sql_arimaa_10_init.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_20_support.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_30_procedures.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_40_triggers.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_50_events.sql;

USE `arimaadockermysqldb`;
'@

$ddl | mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root

# Optionally import the data dump (if present)
if (-not $SkipDataDump) {
  $dataPath = Join-Path $PSScriptRoot 'Documents\Database\mysql\Sql_arimaa_xx_data.sql'
  if (Test-Path -LiteralPath $dataPath) {
    Write-Host "[4/4] Seeding data from Sql_arimaa_xx_data.sql..." -ForegroundColor Cyan
    $data = Get-Content -LiteralPath $dataPath -Raw

    # Ensure we target the expected schema, and robustly skip any GTID_PURGED line which may not be allowed
    # Avoid PowerShell interpreting backticks around the DB name by not using them here (not needed for this identifier)
    $header = "USE arimaadockermysqldb;`n"

    # Some dumps render the line as: SET @@GLOBAL.GTID_PURGED=/*!80000 '+'*/ '...';
    # Previous regex missed this form. Remove any line containing GTID_PURGED (case-insensitive).
    $lines = $data -split "(`r`n|`n|`r)"
    $filteredLines = $lines | Where-Object { $_ -notmatch '(?i)\bGTID_PURGED\b' }
    $dataSanitized = ($filteredLines -join "`n")

    ($header + $dataSanitized) | mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root
  }
  else {
    Write-Host "Data dump not found at $dataPath. Skipping data seed." -ForegroundColor Yellow
  }
}
else {
  Write-Host "-SkipDataDump specified: not importing Sql_arimaa_xx_data.sql" -ForegroundColor Yellow
}

Remove-Item Env:MYSQL_PWD

Write-Host "Database create-and-seed completed." -ForegroundColor Green
