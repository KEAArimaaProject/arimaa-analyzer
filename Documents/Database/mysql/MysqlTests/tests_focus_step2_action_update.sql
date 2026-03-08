-- Step 3 [ACTION]: DELETE match to fire trg_matches_after_delete
USE `arimaadockermysqldb`;

SELECT '[ACTION] DELETE match 669147' AS label;
DELETE FROM Matches
WHERE id = 669147;

-- After-delete trigger should decrement both players that were in the deleted match
SELECT '[CHECK] After trg_matches_after_delete' AS label;
SELECT id, username, games_played FROM Players WHERE id IN (1,4,2,3) ORDER BY id;

SELECT '[CHECK] audit_log (last few rows)' AS label;
SELECT id, table_name, operation, new_value, changed_at
FROM (
         SELECT id, table_name, operation, new_value, changed_at
         FROM audit_log
         ORDER BY id DESC
             LIMIT 5
     ) AS last5
ORDER BY id ASC;
