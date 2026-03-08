# Run the full MySQL test suite using Docker and the local MySQL client
#
# Usage (from project root):
#   powershell -ExecutionPolicy Bypass -File .\run_mysql_tests.ps1
#   # or, in an existing PS session:
#   .\run_mysql_tests.ps1

$ErrorActionPreference = 'Stop'

# Ensure we execute from the script's directory (expected to be the repo root)
Set-Location -LiteralPath $PSScriptRoot

Write-Host "[1] Starting MySQL with docker compose..." -ForegroundColor Cyan
docker compose -f .\Documents\Database\docker-compose.yml up -d

# Pause to allow manual inspection (e.g., in DataGrip)
Read-Host -Prompt "Break — Docker Compose is up. Open DataGrip if desired. Press Enter to continue to readiness check..."

Write-Host "[2] Waiting for MySQL inside the container to be ready..." -ForegroundColor Cyan
docker exec -e MYSQL_PWD=123456 arimaadockermysqldb sh -lc 'until mysqladmin ping -h 127.0.0.1 -uroot --silent; do sleep 1; done'

# Pause before SETUP (reset + seed) so you can inspect the empty/initial state in DataGrip
Read-Host -Prompt "Break — About to run SETUP (reset schema + minimal seed). Press Enter to execute setup..."

Write-Host "[3] Running SETUP (reset schema + minimal seed)..." -ForegroundColor Cyan
$env:MYSQL_PWD = '123456'


Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_step0_reset_and_seed.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root

# Break before each ACTION so you can observe state in DataGrip first
Read-Host -Prompt "Break — [ACTION] Insert match 669147. Press Enter to execute INSERT..."
Write-Host "[4] Executing ACTION: Insert match 669147" -ForegroundColor Cyan
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_step1_action_insert.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root

Read-Host -Prompt "Break — [ACTION] DELETE match 669147. Press Enter to execute DELETE..."
Write-Host "[6] Executing ACTION: DELETE match 669147" -ForegroundColor Cyan
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_step2_action_update.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root

# Break — Demonstrate MySQL scheduled event: ev_cleanup_audit_log (manual simulation of event body)
Read-Host -Prompt "Break — [EVENT] Demo ev_cleanup_audit_log. Press Enter to run events listing and BEFORE-cleanup checks..."
Write-Host "[7] Executing EVENT demo (phase A): list events and prepare BEFORE-cleanup state" -ForegroundColor Cyan
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_events.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root

# New break right before running the cleanup DELETE step
Read-Host -Prompt "Break — [EVENT] About to run CLEANUP (DELETE old audit rows). Press Enter to execute cleanup..."
Write-Host "[7b] Executing EVENT demo (phase B): run cleanup and AFTER-checks" -ForegroundColor Cyan
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_events_cleanup.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root

# Final pause to allow reviewing effects in DataGrip before finishing
Read-Host -Prompt "Final Break — All actions (including EVENT cleanup demo) executed. Review results in DataGrip, then press Enter to complete..."

Remove-Item Env:MYSQL_PWD

Write-Host "All steps completed (script finished)." -ForegroundColor Green
