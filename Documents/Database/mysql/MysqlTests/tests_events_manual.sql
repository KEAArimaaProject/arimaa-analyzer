-- Manual execution of event bodies for deterministic testing
USE `arimaadockermysqldb`;

-- Prepare: insert an old audit_log row to demonstrate cleanup
INSERT INTO audit_log(table_name, operation, record_id, old_value, new_value, changed_at)
VALUES ('__TEST__', 'DELETE', 0, NULL, NULL, NOW() - INTERVAL 2 YEAR);

SELECT '[EVT] audit_log count BEFORE cleanup' AS label; SELECT COUNT(*) AS cnt FROM audit_log;

-- Cleanup audit log older than 1 year (event body)
DELETE FROM audit_log WHERE changed_at < DATE_SUB(NOW(), INTERVAL 1 YEAR);

SELECT '[EVT] audit_log count AFTER cleanup' AS label; SELECT COUNT(*) AS cnt FROM audit_log;

-- Daily match stats upsert for today using sargable range; use REPLACE to avoid deprecated VALUES()
REPLACE INTO daily_match_stats (stat_date, total_matches)
SELECT CURDATE() AS stat_date,
       COUNT(*)   AS total_matches
FROM Matches
WHERE `timestamp` >= CURDATE() AND `timestamp` < (CURDATE() + INTERVAL 1 DAY);

SELECT '[EVT] daily_match_stats for today' AS label; SELECT * FROM daily_match_stats WHERE stat_date = CURDATE();

-- Recalculate games_played (event calls this proc daily)
CALL recalc_games_played();
SELECT '[EVT] Players after recalc_games_played' AS label; SELECT id, username, games_played FROM Players ORDER BY id;
