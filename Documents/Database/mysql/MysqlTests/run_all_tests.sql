-- Usage (PowerShell from project root):
--   mysql --protocol=tcp -h 127.0.0.1 -P 3306 -u root -p < Documents/Database/mysql/MysqlTests/run_all_tests.sql

-- Ensure errors stop execution to surface failures early
SET sql_notes = 1;

-- 1) Reset schema and (re)build all DB objects from existing repo scripts
SELECT 'STEP 1: Reset and build schema/objects' AS step;
SOURCE Documents/Database/mysql/MysqlTests/reset_and_build.sql;

-- 2) Seed deterministic test data
SELECT 'STEP 2: Seed data' AS step;
SOURCE Documents/Database/mysql/MysqlTests/seed_data.sql;

-- 3) Test stored procedures
SELECT 'STEP 3: Procedures' AS step;
SOURCE Documents/Database/mysql/MysqlTests/tests_procedures.sql;

-- 4) Test triggers
SELECT 'STEP 4: Triggers' AS step;
SOURCE Documents/Database/mysql/MysqlTests/tests_triggers.sql;

-- 5) Test events (manual execution of event bodies)
SELECT 'STEP 5: Events (manual bodies)' AS step;
SOURCE Documents/Database/mysql/MysqlTests/tests_events_manual.sql;

SELECT 'ALL STEPS COMPLETED' AS done;
