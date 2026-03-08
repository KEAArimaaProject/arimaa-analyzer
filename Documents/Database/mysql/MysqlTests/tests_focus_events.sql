-- Events Demo: Showcase MySQL scheduled events defined in Sql_arimaa_50_events.sql
-- This script lists the events and manually runs their bodies for a deterministic demo.

USE `arimaadockermysqldb`;

-- Show concise scheduler status and events list (no noisy columns)
SELECT '[EVENTS DEMO] Temporarily turning event_scheduler OFF, then testing Cleanup event' AS label;
SET GLOBAL event_scheduler = OFF;
SHOW VARIABLES LIKE 'event_scheduler';

-- ACTION 1: Simulate ev_cleanup_audit_log
SELECT '[ACTION] Simulate ev_cleanup_audit_log (insert >1y old row, then cleanup)' AS label;

-- Insert an intentionally old audit_log record that should be removed by the cleanup logic
INSERT INTO audit_log (table_name, operation, record_id, old_value, new_value, changed_at)
VALUES ('Demo', 'INSERT', -1, NULL, NULL, DATE_SUB(NOW(), INTERVAL 2 YEAR));


SELECT '[CHECK] audit_log (last few rows)' AS label;
SELECT id, table_name, operation, new_value, changed_at
FROM (
         SELECT id, table_name, operation, new_value, changed_at
         FROM audit_log
         ORDER BY id DESC
             LIMIT 5
     ) AS last5
ORDER BY id ASC;



-- Pause here in the runner before executing the cleanup step
SELECT '[PAUSE] Ready to run cleanup step in next phase' AS label;
