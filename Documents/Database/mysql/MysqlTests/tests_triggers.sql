-- Trigger tests: integrity, aggregates, and audit
USE `arimaadockermysqldb`;

SELECT '[TRG] Start state: players and summaries' AS label;
SELECT id, username, games_played FROM Players ORDER BY id;
SELECT * FROM match_summary ORDER BY gametype;

-- A) Prevent self‑play (BEFORE INSERT/UPDATE) using exception handler to keep script running
DELIMITER $$
DROP PROCEDURE IF EXISTS try_selfplay$$
CREATE PROCEDURE try_selfplay()
BEGIN
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
  BEGIN
    SELECT '[TRG] Self-play prevented as expected' AS label;
  END;

  INSERT INTO Matches(id, termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id)
  VALUES (200, 'resign', 10, 10, 'LOSS', 1, (SELECT id FROM GameTypes WHERE name='Classic'));
END$$
DELIMITER ;

CALL try_selfplay();
DROP PROCEDURE IF EXISTS try_selfplay;

-- B) Timestamp defaulting (BEFORE INSERT)
INSERT INTO Matches(id, termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id, `timestamp`)
VALUES (201, 'timeout', 11, 10, 'WIN', 2, (SELECT id FROM GameTypes WHERE name='Blitz'), NULL);
SELECT '[TRG] Timestamp defaulted' AS label, `timestamp` FROM Matches WHERE id=201;

-- C) Aggregates and audit on INSERT are covered by above insert
SELECT '[TRG] Players after insert 201' AS label; SELECT id, games_played FROM Players WHERE id IN (10,11) ORDER BY id;
SELECT '[TRG] match_summary after insert 201' AS label; SELECT * FROM match_summary ORDER BY gametype;
SELECT '[TRG] audit INSERT 201' AS label; SELECT id, table_name, operation, record_id FROM audit_log WHERE table_name='Matches' AND operation='INSERT' AND record_id=201 ORDER BY id DESC LIMIT 1;

-- D) Rebalance summary on UPDATE (change GameType for 100)
UPDATE Matches SET gameTypes_id = (SELECT id FROM GameTypes WHERE name='Blitz') WHERE id = 100;
SELECT '[TRG] match_summary after update 100->Blitz' AS label; SELECT * FROM match_summary ORDER BY gametype;
SELECT '[TRG] audit UPDATE 100' AS label; SELECT id, table_name, operation, record_id FROM audit_log WHERE table_name='Matches' AND operation='UPDATE' AND record_id=100 ORDER BY id DESC LIMIT 1;

-- E) Aggregates and audit on DELETE (handle children first due to FKs)
DELETE FROM Moves WHERE matches_id = 101;
DELETE FROM OpeningsByMatch WHERE matches_id = 101;
DELETE FROM Matches WHERE id = 101;

SELECT '[TRG] Players after delete 101' AS label; SELECT id, games_played FROM Players WHERE id IN (10,11) ORDER BY id;
SELECT '[TRG] match_summary after delete 101' AS label; SELECT * FROM match_summary ORDER BY gametype;
SELECT '[TRG] audit DELETE 101' AS label; SELECT id, table_name, operation, record_id FROM audit_log WHERE table_name='Matches' AND operation='DELETE' AND record_id=101 ORDER BY id DESC LIMIT 1;

-- F) Moves validation triggers using exception handlers
DELIMITER $$
DROP PROCEDURE IF EXISTS try_invalid_move_dir$$
CREATE PROCEDURE try_invalid_move_dir()
BEGIN
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
  BEGIN
    SELECT '[TRG] Invalid move direction rejected as expected' AS label;
  END;
  INSERT INTO Moves(turn, sequence, direction, status, matches_id, position_id)
  VALUES (2,1,'Q','1',100,1);
END$$
DELIMITER ;
CALL try_invalid_move_dir();
DROP PROCEDURE IF EXISTS try_invalid_move_dir;

DELIMITER $$
DROP PROCEDURE IF EXISTS try_invalid_move_status$$
CREATE PROCEDURE try_invalid_move_status()
BEGIN
  DECLARE CONTINUE HANDLER FOR SQLEXCEPTION
  BEGIN
    SELECT '[TRG] Invalid move status rejected as expected' AS label;
  END;
  INSERT INTO Moves(turn, sequence, direction, status, matches_id, position_id)
  VALUES (2,1,'n','2',100,1);
END$$
DELIMITER ;
CALL try_invalid_move_status();
DROP PROCEDURE IF EXISTS try_invalid_move_status;

-- Valid update should pass
UPDATE Moves SET direction='w' WHERE matches_id=100 AND sequence=1 LIMIT 1;
SELECT '[TRG] Moves valid update ok' AS label; SELECT matches_id, turn, sequence, direction FROM Moves WHERE matches_id=100 ORDER BY id;
