
-- Run the following once per test cycle to start from a known state.

-- Manually verify that it works: 
    
-- SELECT * FROM match_summary; 
-- should reflect two inserts via triggers 
-- (see section 3 tests for exact expectations).
    
-- SELECT id, games_played FROM Players WHERE id IN (10,11); 
-- should be incremented to reflect the two recent matches.
    

-- 1.1 Drop and recreate schema (if you have no data to preserve)
DROP SCHEMA IF EXISTS `arimaadockermysqldb`;
CREATE SCHEMA `arimaadockermysqldb` DEFAULT CHARACTER SET utf8;
USE `arimaadockermysqldb`;

-- 1.2 Execute your DDL/routine files in order (via client or Workbench)
--    Sql_arimaa_10_init.sql
--    Sql_arimaa_20_support.sql
--    Sql_arimaa_30_procedures.sql
--    Sql_arimaa_40_triggers.sql
--    Sql_arimaa_50_events.sql

-- 1.3 Seed lookup tables
INSERT INTO Countries(id, name) VALUES (1,'Norway'), (2,'USA');
INSERT INTO GameTypes(name, time_increment, time_reserve) VALUES
                                                              ('Classic',60,300), ('Blitz',5,60);
INSERT INTO Events(id, name, start_date) VALUES (1,'Winter Cup','2026-01-10'), (2,'Spring Open','2026-03-01');

-- 1.4 Seed players (note: passwords here are placeholder test values)
INSERT INTO Players(id, username, email, password, rating, RU, games_played, countries_id)
VALUES
    (10,'Alice','alice@example.com','test',1700, NULL, 0, 1),
    (11,'Bob','bob@example.com','test',1600, NULL, 0, 2),
    (12,'Carol','carol@example.com','test',NULL, NULL, 0, 1);

-- 1.5 Seed positions (minimal for valid Moves rows)
INSERT INTO Position(color, piece, cordinate) VALUES
                                                  ('gold','R','a1'), ('silver','r','h8');

-- 1.6 Create matches covering multiple scenarios (Classic/Blitz, today and old)
-- Note: let BEFORE INSERT trigger default timestamp if NULL
INSERT INTO Matches(id, termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id, `timestamp`)
VALUES
    (100, 'checkmate', 10, 11, 'WIN', 1, 1, NOW()),
    (101, 'timeout',   11, 10, 'LOSS', 2, 2, NOW()),
    (102, 'resign',    10, 11, 'WIN',  1, 1, NOW() - INTERVAL 400 DAY); -- old match for archiving tests

-- 1.7 Seed minimal moves per match to validate relations and triggers
INSERT INTO Moves(turn, sequence, direction, status, matches_id, position_id)
VALUES
    (1, 1, 'n', '1', 100, 1),
    (1, 2, 'e', '1', 100, 2),
    (1, 1, 'w', '1', 101, 1);

-- 1.8 Seed an opening reference (optional)
INSERT INTO OpeningsByMatch(matches_id, position_id) VALUES (100,1);