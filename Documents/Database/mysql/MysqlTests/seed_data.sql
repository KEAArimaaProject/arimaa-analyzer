-- Seed deterministic test data for procedures/trigger demos
USE `arimaadockermysqldb`;

-- Clean tables (respect FK order)
SET FOREIGN_KEY_CHECKS = 0;
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
SET FOREIGN_KEY_CHECKS = 1;

-- Lookups
INSERT INTO Countries(id, name) VALUES (1,'Norway'), (2,'USA');
INSERT INTO GameTypes(name, time_increment, time_reserve) VALUES
  ('Classic',60,300), ('Blitz',5,60);
INSERT INTO Events(id, name, start_date) VALUES (1,'Winter Cup','2026-01-10'), (2,'Spring Open','2026-03-01');

-- Players
INSERT INTO Players(id, username, email, password, rating, RU, games_played, countries_id)
VALUES
  (10,'Alice','alice@example.com','test',1700, NULL, 0, 1),
  (11,'Bob','bob@example.com','test',1600, NULL, 0, 2),
  (12,'Carol','carol@example.com','test',NULL, NULL, 0, 1);

-- Positions (minimal rows for Moves)
INSERT INTO Position(color, piece, cordinate) VALUES
  ('gold','R','a1'), ('silver','r','h8');

-- Matches (two recent, one old)
INSERT INTO Matches(id, termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id, `timestamp`)
VALUES
  (100, 'checkmate', 10, 11, 'WIN', 1, (SELECT id FROM GameTypes WHERE name='Classic'), NOW()),
  (101, 'timeout',   11, 10, 'LOSS', 2, (SELECT id FROM GameTypes WHERE name='Blitz'),   NOW()),
  (102, 'resign',    10, 11, 'WIN',  1, (SELECT id FROM GameTypes WHERE name='Classic'), NOW() - INTERVAL 400 DAY);

-- Moves
INSERT INTO Moves(turn, sequence, direction, status, matches_id, position_id)
VALUES
  (1, 1, 'n', '1', 100, 1),
  (1, 2, 'e', '1', 100, 2),
  (1, 1, 'w', '1', 101, 1);

-- Opening reference (optional)
INSERT INTO OpeningsByMatch(matches_id, position_id) VALUES (100,1);

-- Quick visibility
SELECT '[SEED] Players' AS label; SELECT id, username, rating, games_played FROM Players ORDER BY id;
SELECT '[SEED] Matches' AS label; SELECT id, gameTypes_id, `timestamp` FROM Matches ORDER BY id;
SELECT '[SEED] match_summary (after triggers)' AS label; SELECT * FROM match_summary ORDER BY gametype;
