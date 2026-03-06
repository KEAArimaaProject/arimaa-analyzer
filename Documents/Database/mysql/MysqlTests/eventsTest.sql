
--  Test events

-- Two approaches: 
-- (A) run event bodies manually for deterministic demos; 
-- (B) enable scheduler and observe automatic execution.



-- A) Manual execution of event bodies (recommended for demo)

-- 4.1 Cleanup audit log older than 1 year (simulate event)
DELETE FROM audit_log WHERE changed_at < DATE_SUB(NOW(), INTERVAL 1 YEAR);

-- 4.2 Daily match stats (use sargable window)
INSERT INTO daily_match_stats (stat_date, total_matches)
SELECT CURDATE() AS stat_date, COUNT(*) AS total_matches
FROM Matches
WHERE `timestamp` >= CURDATE() AND `timestamp` < CURDATE() + INTERVAL 1 DAY
    AS new
ON DUPLICATE KEY UPDATE total_matches = new.total_matches;
SELECT * FROM daily_match_stats WHERE stat_date = CURDATE();

-- 4.3 Recalculate games played
CALL recalc_games_played();
SELECT id, games_played FROM Players WHERE id IN (10,11,12);



-- B) Automatic execution via MySQL Event Scheduler

-- Enable scheduler for the session/instance
SET GLOBAL event_scheduler = ON;

-- For a live demo, temporarily change schedules to EVERY 1 MINUTE (optional) or just wait for daily run.
-- Then watch tables:
SELECT * FROM daily_match_stats WHERE stat_date = CURDATE(); -- should upsert after the scheduled run
-- and confirm no errors in performance_schema.events_statements_summary_by_digest or the MySQL error log.


-- Verification:
-- daily_match_stats has a row for today with correct count.
-- Old audit_log rows are cleared when predicate matches.
-- recalc_games_played recomputes expected counters idempotently.



                                                                       
                                                                       
                                                                       
                                                                       







