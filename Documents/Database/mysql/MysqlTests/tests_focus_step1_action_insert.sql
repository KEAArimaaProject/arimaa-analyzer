-- Step 1 [ACTION]: Insert the specified match
USE `arimaadockermysqldb`;

SELECT '[ACTION] Insert match 669147' AS label;
INSERT INTO Matches (
  id, termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id, `timestamp`
)
VALUES (
  669147, 't', 1, 4, 'w', 1, 83, '2026-02-02 06:22:59'
);

-- After-insert trigger increments games_played for players 1 and 4
SELECT '[CHECK] After insert (trg_matches_after_insert effects)' AS label;
SELECT id, username, games_played
FROM Players
WHERE id IN (1,4,2,3)
ORDER BY id;

-- check Audit
SELECT '[CHECK] audit_log' AS label; SELECT id, table_name, operation, new_value, changed_at FROM audit_log ORDER BY id;


