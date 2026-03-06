
-- Optional automation script (single-command demo)

-- You can bundle the setup and tests into a single 
-- .sql runner to execute from mysql CLI or Workbench. 
-- Example CLI invocation:

-- # PowerShell (adjust credentials/host)
-- mysql --protocol=tcp -h 127.0.0.1 -P 3306 -u root -p < run_all_tests.sql


-- Contents to place in run_all_tests.sql:
-- Includes the schema/routine files in order
-- The seed data from section 1
-- The proc calls from section 2 (with SELECT checks)
-- The trigger exercises from section 3
-- The manual event body invocations from section 4A
-- Final SELECT blocks that print pass/fail‑like summaries 
-- (e.g., counts equal to expected numbers)
-- Tip: Add [DEBUG_LOG]-style comments and SELECT 
-- outputs with labels to make console output easy to read in demos.
      
-- Tip: Add [DEBUG_LOG]-style comments and SELECT outputs with labels to 
-- make console output easy to read in demos.



/* 
-- Quick reset for demo loops
TRUNCATE TABLE Moves;
TRUNCATE TABLE OpeningsByMatch;
TRUNCATE TABLE Matches;
TRUNCATE TABLE Players;
TRUNCATE TABLE Position;
TRUNCATE TABLE match_summary;
TRUNCATE TABLE audit_log;
TRUNCATE TABLE daily_match_stats;
TRUNCATE TABLE Countries;
TRUNCATE TABLE GameTypes;
TRUNCATE TABLE Events;
-- Then rerun section 1.3–1.8 seeding steps
*/

-- What to watch for during testing

/* 
Foreign key constraints when deleting Matches with 
dependent Moves/OpeningsByMatch.
audit_log growth; ensure purges are performant 
(consider adding INDEX(changed_at)).
MySQL version differences for VALUES() in 
ON DUPLICATE KEY UPDATE (use alias approach in manual tests).
Case/enum consistency for direction, status, 
match_result, and termination_type.
*/                                                       

                       

                                                           
                                                           

