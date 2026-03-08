### MySQL Focused Test Instructions

This folder contains a focused, step-by-step database demo to exercise key stored objects using an interactive PowerShell runner. It rebuilds the schema, loads deterministic seed data, performs one insert, calls one stored procedure, deletes the inserted row, and demonstrates a scheduled event.

### Prerequisites
- Docker Desktop running
- Local MySQL client available on PATH (`mysql`, `mysqladmin`)
- Project root: `C:\Users\USER\MYPROJECTS\arimaa-analyzer`
- Container name: `arimaadockermysqldb`
- Host/port for MySQL client: `127.0.0.1:3307`
- Credentials: user `root`, password `123456`

### Quick start (recommended)
From the project root, run the runner script. It is interactive and pauses between steps for inspection in your SQL client.

PowerShell:
```
& .\run_mysql_tests.ps1
```

### Running the ordinary database:
```
& .\run_mysql_database.ps1
```

### Scripts in this folder
- `reset_and_build.sql`
  - Rebuilds the schema and stored objects by sourcing: `Sql_arimaa_10_init.sql`, `Sql_arimaa_20_support.sql`, `Sql_arimaa_30_procedures.sql`, `Sql_arimaa_40_triggers.sql`, `Sql_arimaa_50_events.sql`.
- `seed_data.sql`
  - Loads deterministic seed data for stable outputs.
- `tests_focus_step0_reset_and_seed.sql`
  - Orchestrates reset/build + seed and ensures `GameTypes(id=83)` exists for the demo match.
- `tests_focus_step1_action_insert.sql`
  - Inserts match `669147` and shows affected players.
- `tests_focus_step2_action_update.sql`
  - Deletes match `669147` and shows affected state (including recent `audit_log`).
- `tests_focus_events.sql` (Phase A)
  - Turns the event scheduler OFF and shows its status, inserts an intentionally old `audit_log` row (>1y), and displays BEFORE counts. Ends with a pause label.
- `tests_focus_events_cleanup.sql` (Phase B)
  - Restores the scheduler to ON after a 5s countdown, temporarily sets `ev_cleanup_audit_log` to run `EVERY 10 SECOND`, waits ~12s, shows `LAST_EXECUTED`, verifies the old row is gone, and restores the schedule to `EVERY 30 DAY`.

### What the runner executes (step-by-step)
`run_mysql_tests.ps1` performs these actions:
1. Start MySQL via Docker Compose (`.\Documents\Database\docker-compose.yml`).
2. Pause to allow opening your SQL client.
3. Wait until MySQL in the container responds to ping.
4. Pause before setup.
5. Run setup (reset/build + seed): `tests_focus_step0_reset_and_seed.sql`.
6. Pause.
7. Insert the demo match: `tests_focus_step1_action_insert.sql`.
8. Pause.
11. Delete the demo match: `tests_focus_step2_action_update.sql`.
12. Pause.
13. Events demo Phase A (scheduler OFF, BEFORE checks): `tests_focus_events.sql`.
14. Pause.
15. Events demo Phase B (scheduler ON, 10-second cadence, AFTER checks, revert): `tests_focus_events_cleanup.sql`.
16. Final pause, then exit.

Connection details used by the runner:
- Host `127.0.0.1`, port `3307`
- User `root` (password provided via env var `MYSQL_PWD=123456` during execution)

### Stored objects actually exercised
- Triggers (via insert/delete and procedure side-effects)
  - `trg_matches_before_insert`
  - `trg_matches_after_insert`
  - `trg_matches_after_delete`
  - `trg_players_update_audit` (fires when player rows are updated)
- Events
  - `ev_cleanup_audit_log` (demonstrated in Phase B by temporarily scheduling it `EVERY 10 SECOND`, then restored to `EVERY 30 DAY`)

Notes on visibility in output:
- INSERT audit entries show only `new_value`.
- UPDATE audit entries show `old_value` and `new_value` (often identical in this focused demo).
- DELETE audit entries show only `old_value` (e.g., deletion of match `669147`).

### Not covered by the focused flow (available in schema)
- Events: `ev_recalculate_games`, `ev_daily_match_stats` (installed but not demonstrated here).
- Triggers: `trg_matches_before_update`, `trg_matches_after_update`, `trg_moves_before_insert_validate`, `trg_moves_before_update_validate` (not exercised by these steps).
- Additional procedures defined in `Sql_arimaa_30_procedures.sql` (only `recalc_games_played()` is exercised here).

### Optional: run pieces manually
If you prefer to run a subset manually using the host MySQL client:
```
$env:MYSQL_PWD = '123456'
# Reset + seed
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_step0_reset_and_seed.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root
# Insert match
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_step1_action_insert.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root
# Delete match
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_step2_action_update.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root
# Events demo (A then B)
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_events.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root
Get-Content .\Documents\Database\mysql\MysqlTests\tests_focus_events_cleanup.sql -Raw |
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root
Remove-Item Env:MYSQL_PWD
```

### Troubleshooting
- If connection fails, ensure Docker is running and you’re using host port `3307`.
- If the scheduler appears OFF during Phase B, verify privileges and that `SET GLOBAL event_scheduler = ON` succeeded.
- If the 10-second event demo doesn’t show `LAST_EXECUTED` updating, wait a few more seconds and rerun the status `SELECT` in Phase B, or confirm the event exists in `information_schema.EVENTS`.

