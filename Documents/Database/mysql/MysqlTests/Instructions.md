### Goal
Provide a clear, repeatable plan to run the SQL in `Documents/Database/mysql/MysqlTests` 
to test stored procedures, triggers, and events, using either a local MySQL client or only Docker.

### Preconditions
- MySQL runs in Docker as per `Documents/Install.md`:
    - Container: `arimaadockermysqldb`
    - Host: `localhost`
    - Port: `3307` (host) → `3306` (container)
    - User: `root`, Password: `123456`
    - Default DB: `arimaadockermysqldb`
- Run commands from project root: `C:\Users\CMLyk\RiderProjects\arimaa-analyzer`
- Important: When connecting from the host, use port `3307` (not `3306`). Some comments inside scripts show `3306`; prefer the `Install.md` values.

### What the test files do
- `MysqlTests/reset_and_build.sql`: Drops and rebuilds schema/objects by sourcing your base scripts (`Sql_arimaa_*`).
- `MysqlTests/seed_data.sql`: Seeds deterministic data across key tables to make test outputs stable.
- `MysqlTests/tests_procedures.sql`: Calls your stored procedures with readable labels.
- `MysqlTests/tests_triggers.sql`: Exercises trigger behaviors and shows resulting rows.
- `MysqlTests/tests_events_manual.sql`: Manually executes event bodies (or equivalent) to validate event logic without waiting for the scheduler.
- `MysqlTests/run_all_tests.sql`: Orchestrates the above in order: reset/build → seed → procedures → triggers → events.

### Option A — Use local MySQL client (PowerShell)
- Full suite (recommended):
  ```powershell
  # From project root
  Get-Content .\Documents\Database\mysql\MysqlTests\run_all_tests.sql |   mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root -p123456
  
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root -p123456 ^ < .\Documents\Database\mysql\MysqlTests\run_all_tests.sql
  ```
  Expected flow in output:
    - STEP 1: Reset and build schema/objects
    - STEP 2: Seed data (+ small SELECT previews)
    - STEP 3: Procedures (each block prefixed by [PROC] label)
    - STEP 4: Triggers
    - STEP 5: Events (manual bodies)
    - ALL STEPS COMPLETED

- Run only procedures (fresh build + seed):
  ```powershell
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root -p123456 ^
    -e "SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\reset_and_build.sql; \
        SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\seed_data.sql; \
        SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\tests_procedures.sql;"
  ```

- Run only triggers (fresh build + seed):
  ```powershell
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root -p123456 ^
    -e "SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\reset_and_build.sql; \
        SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\seed_data.sql; \
        SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\tests_triggers.sql;"
  ```

- Run only events (fresh build + seed):
  ```powershell
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root -p123456 ^
    -e "SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\reset_and_build.sql; \
        SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\seed_data.sql; \
        SOURCE .\\Documents\\Database\\mysql\\MysqlTests\\tests_events_manual.sql;"
  ```

### Option B — No host client; use only Docker
- Pipe the SQL into the containerized client (keeps host clean):
  ```powershell
  Get-Content .\Documents\Database\mysql\MysqlTests\run_all_tests.sql | \
    docker exec -i arimaadockermysqldb mysql -uroot -p123456
  ```
  Notes:
    - When using `docker exec ... mysql -e "SOURCE ..."`, the path is inside the container. Piping the file avoids path issues.

- Alternatively, copy then SOURCE inside container:
  ```powershell
  docker cp .\Documents\Database\mysql\MysqlTests\run_all_tests.sql arimaadockermysqldb:/tmp/run_all_tests.sql
  docker exec -it arimaadockermysqldb mysql -uroot -p123456 -e "SOURCE /tmp/run_all_tests.sql"
  ```

### Interpreting results
- Each section prints a label row first (e.g., `STEP 3: Procedures`, or `[PROC] get_total_players`).
- Failures:
    - Any SQL error (e.g., missing object, wrong arguments) will appear with an error code and typically stop subsequent statements in that `SOURCE`d file from running.
    - Use the printed step/label to locate which script and block failed.
- Success criteria:
    - The final line `ALL STEPS COMPLETED` indicates all scripts executed without SQL errors.

### Resetting between runs
- The orchestrated scripts already drop and rebuild the schema at the start (`reset_and_build.sql`). You don’t need to manually wipe tables between runs.
- If you run subsets repeatedly, include `reset_and_build.sql` and `seed_data.sql` each time to keep tests deterministic.

### Troubleshooting
- Port in use / connection refused: Ensure Docker is running, and you connect to host port `3307`.
- Access denied: Confirm `-u root -p123456` matches your container setup.
- `SOURCE` path errors: Prefer the full orchestrator `run_all_tests.sql` with stdin redirection or the `-e "SOURCE ..."` approach using Windows-escaped backslashes; for Docker, pipe files or copy them into `/tmp` first.
- Initial seed didn’t run on container startup: Not required for tests; the test flow recreates everything.

### Optional: One-liner batch helpers
- Create a `run_mysql_tests.bat` in project root with:
  ```bat
  @echo off
  mysql --protocol=tcp -h 127.0.0.1 -P 3307 -u root -p123456 ^
    < .\Documents\Database\mysql\MysqlTests\run_all_tests.sql
  ```

### Summary
- Preferred: run the full suite with a single command (Option A or B).
- For focused checks, run build+seed plus the specific `tests_*.sql` file.
- Use the printed step/label blocks to read outputs and pinpoint any failures.
