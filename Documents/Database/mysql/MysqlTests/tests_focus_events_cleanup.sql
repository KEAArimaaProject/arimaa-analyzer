-- Events Demo (Phase B): Execute cleanup and show AFTER-checks

USE `arimaadockermysqldb`;

-- 5-second countdown before restoring the scheduler to ON
SELECT '[EVENTS DEMO] Countdown (8s) before re-enabling event_scheduler' AS label;
SELECT '8' AS seconds_remaining; SELECT SLEEP(1);
SELECT '7' AS seconds_remaining; SELECT SLEEP(1);
SELECT '6' AS seconds_remaining; SELECT SLEEP(1);
SELECT '5' AS seconds_remaining; SELECT SLEEP(1);
SELECT '4' AS seconds_remaining; SELECT SLEEP(1);
SELECT '3' AS seconds_remaining; SELECT SLEEP(1);
SELECT '2' AS seconds_remaining; SELECT SLEEP(1);
SELECT '1' AS seconds_remaining; SELECT SLEEP(1);

-- Restore scheduler to ON so subsequent demos keep expected behavior
SELECT '[EVENTS DEMO] Restoring event_scheduler to ON' AS label;
SET GLOBAL event_scheduler = ON;

-- Temporarily reschedule ev_cleanup_audit_log to run quickly for the live demo
SELECT '[EVENTS DEMO] Temporarily setting ev_cleanup_audit_log to run EVERY 10 SECOND' AS label;
ALTER EVENT ev_cleanup_audit_log ON SCHEDULE EVERY 10 SECOND ENABLE;

-- Wait ~12 seconds to allow the event to execute at least once
SELECT '[EVENTS DEMO] Waiting ~5 seconds to allow the 10s event to fire' AS label;
SELECT '5' AS seconds_remaining; SELECT SLEEP(1);
SELECT '4' AS seconds_remaining; SELECT SLEEP(1);
SELECT '3' AS seconds_remaining; SELECT SLEEP(1);
SELECT '2' AS seconds_remaining; SELECT SLEEP(1);
SELECT '1' AS seconds_remaining; SELECT SLEEP(1);
      
-- Show concise event info (interval + last executed) before we wait
SELECT '[EVENTS DEMO] Event status BEFORE waiting' AS label;
SELECT 
  EVENT_NAME   AS name,
  STATUS       AS status,
  INTERVAL_VALUE AS interval_value,
  INTERVAL_FIELD AS interval_field,
  LAST_EXECUTED  AS last_executed
FROM information_schema.EVENTS
WHERE EVENT_SCHEMA = 'arimaadockermysqldb' AND EVENT_NAME = 'ev_cleanup_audit_log';

-- Show event info AFTER waiting (LAST_EXECUTED should be recent)
SELECT '[EVENTS DEMO] Event status AFTER waiting' AS label;
SELECT 
  EVENT_NAME   AS name,
  STATUS       AS status,
  INTERVAL_VALUE AS interval_value,
  INTERVAL_FIELD AS interval_field,
  LAST_EXECUTED  AS last_executed
FROM information_schema.EVENTS
WHERE EVENT_SCHEMA = 'arimaadockermysqldb' AND EVENT_NAME = 'ev_cleanup_audit_log';

-- Now the old demo row (>1y) should have been cleaned by the actual scheduled event
SELECT '[CHECK] audit_log after scheduled cleanup (old_rows should be 0)' AS label;
SELECT 
  COUNT(*) AS total_rows,
  SUM(CASE WHEN changed_at < DATE_SUB(NOW(), INTERVAL 1 YEAR) THEN 1 ELSE 0 END) AS old_rows
FROM audit_log;

-- Revert the event back to its production cadence (EVERY 30 DAY)
SELECT '[EVENTS DEMO] Reverting ev_cleanup_audit_log to EVERY 30 DAY' AS label;
ALTER EVENT ev_cleanup_audit_log ON SCHEDULE EVERY 30 DAY ENABLE;

-- Final confirmation of restored schedule
SELECT '[EVENTS DEMO] Event schedule restored' AS label;
SELECT 
  EVENT_NAME   AS name,
  STATUS       AS status,
  INTERVAL_VALUE AS interval_value,
  INTERVAL_FIELD AS interval_field,
  LAST_EXECUTED  AS last_executed
FROM information_schema.EVENTS
WHERE EVENT_SCHEMA = 'arimaadockermysqldb' AND EVENT_NAME = 'ev_cleanup_audit_log';

SELECT '[DONE] ev_cleanup_audit_log demo complete' AS label;
