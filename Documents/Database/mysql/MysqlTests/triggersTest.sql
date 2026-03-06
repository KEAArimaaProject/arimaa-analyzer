

-- Test triggers

-- Systematically exercise BEFORE/AFTER insert/update/delete logic and validate side effects on aggregates and audit logs.

    
-- Expected outcomes summary for demo:
-- Self‑play insert fails with trigger error.
-- timestamp auto‑filled when NULL.
-- games_played for affected players increments/decrements appropriately and never below zero.
-- match_summary rows reflect correct counts per GameType before/after updates/deletes.
-- audit_log contains JSON snapshots for INSERT/UPDATE/DELETE as triggered.
-- Invalid Moves insertions are rejected; valid updates succeed
    
    

-- A) Prevent self‑play (BEFORE INSERT/UPDATE)

-- Attempt to insert a self-play match (should fail)
INSERT INTO Matches(id, termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id)
VALUES (200, 'resign', 10, 10, 'LOSS', 1, 1);
-- Expect: ERROR with message from trigger


-- B) Timestamp defaulting (BEFORE INSERT)

INSERT INTO Matches(id, termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id, `timestamp`)
VALUES (201, 'timeout', 11, 10, 'WIN', 2, 2, NULL);
SELECT `timestamp` FROM Matches WHERE id=201; -- Expect non-NULL CURRENT_TIMESTAMP

    
-- C) Aggregate updates and audit on INSERT (AFTER INSERT)

-- On creating match 201 above, verify players.games_played incremented and match_summary updated
SELECT id, games_played FROM Players WHERE id IN (10,11);
SELECT * FROM match_summary ORDER BY gametype;  -- Should reflect increments for the involved GameType names
SELECT * FROM audit_log WHERE table_name='Matches' AND operation='INSERT' AND record_id=201; 
-- JSON new_value present


-- D) Aggregate rebalancing and audit on UPDATE (AFTER UPDATE)

-- Change GameType from Classic to Blitz for match 100 to force summary rebalancing
-- First find ids: SELECT id FROM GameTypes WHERE name='Blitz';
UPDATE Matches SET gameTypes_id = (SELECT id FROM GameTypes WHERE name='Blitz') WHERE id = 100;

-- Verify: match_summary decremented for Classic and incremented for Blitz
SELECT * FROM match_summary ORDER BY gametype;
SELECT * FROM audit_log WHERE table_name='Matches' AND operation='UPDATE' AND record_id=100; 
-- JSON old/new values present
                

-- E) Aggregate and audit on DELETE (AFTER DELETE)
DELETE FROM Matches WHERE id = 101;
-- Verify player counters never drop below zero and summaries updated
SELECT id, games_played FROM Players WHERE id IN (10,11);
SELECT * FROM match_summary ORDER BY gametype;
SELECT * FROM audit_log WHERE table_name='Matches' AND operation='DELETE' AND record_id=101;


-- F) Moves validation triggers (BEFORE INSERT/UPDATE)

-- Invalid direction (should fail)
INSERT INTO Moves(turn, sequence, direction, status, matches_id, position_id)
VALUES (2,1,'Q','1',100,1);
-- Invalid status (should fail)
INSERT INTO Moves(turn, sequence, direction, status, matches_id, position_id)
VALUES (2,1,'n','2',100,1);
-- Valid update upheld by trigger
UPDATE Moves SET direction='w' WHERE matches_id=100 AND sequence=1;


-- G) Players update audit

UPDATE Players SET rating = 1750 WHERE id = 10;
SELECT * FROM audit_log WHERE table_name='Players' AND operation='UPDATE' AND record_id=10 ORDER BY changed_at DESC LIMIT 1;




                                                                                             

