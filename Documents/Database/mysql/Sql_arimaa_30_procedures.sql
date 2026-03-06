-- Stored procedures
-- Requires: Sql_arimaa_init.sql, Sql_arimaa_20_support.sql

USE `arimaadockermysqldb`;

DELIMITER $$
DROP PROCEDURE IF EXISTS `insert_match`$$
CREATE PROCEDURE `insert_match`(
    IN p_termination_type VARCHAR(45),
    IN p_player_id_silver INT,
    IN p_player_id_gold INT,
    IN p_match_result VARCHAR(45),
    IN p_events_id INT,
    IN p_gameTypes_id INT,
    IN p_timestamp DATETIME
)
BEGIN
    INSERT INTO Matches (termination_type, player_id_silver, player_id_gold, match_result, events_id, gameTypes_id, timestamp)
    VALUES (p_termination_type, p_player_id_silver, p_player_id_gold, p_match_result, p_events_id, p_gameTypes_id, p_timestamp);
END$$

DROP PROCEDURE IF EXISTS `mark_match_as_corrupted`$$
CREATE PROCEDURE `mark_match_as_corrupted`(
    IN p_match_id INT
)
BEGIN
    UPDATE Matches
    SET isCorrupted = 1
    WHERE id = p_match_id;
END$$


DROP PROCEDURE IF EXISTS `get_top_and_bottom_players`$$
CREATE PROCEDURE `get_top_and_bottom_players`()
BEGIN
    SELECT id, username, rating
    FROM Players
    WHERE rating IS NOT NULL
    ORDER BY rating DESC
    LIMIT 5;

    SELECT id, username, rating
    FROM Players
    WHERE rating IS NOT NULL
    ORDER BY rating ASC
    LIMIT 5;
END$$

DROP PROCEDURE IF EXISTS `get_total_matches`$$
CREATE PROCEDURE `get_total_matches`()
BEGIN
    SELECT COUNT(*) AS total_matches
    FROM Matches;
END$$

DROP PROCEDURE IF EXISTS `get_total_players`$$
CREATE PROCEDURE `get_total_players`()
BEGIN
    SELECT COUNT(*) AS total_players
    FROM Players;
END$$

DROP PROCEDURE IF EXISTS `get_matches_played_by_player`$$
CREATE PROCEDURE `get_matches_played_by_player`(
    IN p_player_id INT
)
BEGIN
    SELECT COUNT(*) AS matches_played
    FROM Matches
    WHERE player_id_silver = p_player_id
       OR player_id_gold = p_player_id;
END$$

DROP PROCEDURE IF EXISTS `get_player_match_stats`$$
CREATE PROCEDURE `get_player_match_stats`(
    IN p_player_id INT
)
BEGIN
    SELECT
        COUNT(*) AS matches_played,
        SUM(CASE WHEN match_result = 'WIN'  THEN 1 ELSE 0 END) AS wins,
        SUM(CASE WHEN match_result = 'LOSS' THEN 1 ELSE 0 END) AS losses
    FROM Matches
    WHERE player_id_silver = p_player_id
       OR player_id_gold = p_player_id;
END$$

DROP PROCEDURE IF EXISTS `get_player_matches_by_gametype`$$
CREATE PROCEDURE `get_player_matches_by_gametype`(
    IN p_player_id INT,
    IN p_gametype VARCHAR(45)
)
BEGIN
    SELECT COUNT(*) AS matches_played
    FROM Matches m
    JOIN GameTypes gt ON gt.id = m.gameTypes_id
    WHERE gt.name = p_gametype
      AND (m.player_id_silver = p_player_id OR m.player_id_gold = p_player_id);
END$$

DROP PROCEDURE IF EXISTS `get_player_count_by_country`$$
CREATE PROCEDURE `get_player_count_by_country`()
BEGIN
    SELECT
        c.name AS country,
        COUNT(p.id) AS player_count
    FROM Countries c
    LEFT JOIN Players p ON c.id = p.countries_id
    GROUP BY c.id, c.name
    ORDER BY player_count DESC;
END$$

DROP PROCEDURE IF EXISTS `get_leaderboard_by_gametype`$$
CREATE PROCEDURE `get_leaderboard_by_gametype`(
    IN p_gametype VARCHAR(45)
)
BEGIN
    SELECT
        p.id,
        p.username,
        COUNT(m.id) AS matches_played
    FROM Players p
    JOIN Matches m ON p.id IN (m.player_id_silver, m.player_id_gold)
    JOIN GameTypes gt ON gt.id = m.gameTypes_id
    WHERE gt.name = p_gametype
    GROUP BY p.id, p.username
    ORDER BY matches_played DESC;
END$$

DROP PROCEDURE IF EXISTS `get_event_participation`$$
CREATE PROCEDURE `get_event_participation`()
BEGIN
    SELECT
        e.name AS event_name,
        COUNT(m.id) AS matches_played
    FROM Events e
    LEFT JOIN Matches m ON e.id = m.events_id
    GROUP BY e.id, e.name;
END$$

DROP PROCEDURE IF EXISTS `get_player_activity_by_month`$$
CREATE PROCEDURE `get_player_activity_by_month`()
BEGIN
    SELECT
        YEAR(`timestamp`) AS `year`,
        MONTH(`timestamp`) AS `month`,
        COUNT(*) AS matches_played
    FROM Matches
    GROUP BY YEAR(`timestamp`), MONTH(`timestamp`)
    ORDER BY `year`, `month`;
END$$

DROP PROCEDURE IF EXISTS `find_invalid_matches`$$
CREATE PROCEDURE `find_invalid_matches`()
BEGIN
    SELECT *
    FROM Matches
    WHERE player_id_silver = player_id_gold;
END$$

DROP PROCEDURE IF EXISTS `find_players_without_rating`$$
CREATE PROCEDURE `find_players_without_rating`()
BEGIN
    SELECT id, username
    FROM Players
    WHERE rating IS NULL;
END$$

DROP PROCEDURE IF EXISTS `find_matches_without_moves`$$
CREATE PROCEDURE `find_matches_without_moves`()
BEGIN
    SELECT m.id
    FROM Matches m
    LEFT JOIN Moves mv ON m.id = mv.matches_id
    WHERE mv.id IS NULL;
END$$

DROP PROCEDURE IF EXISTS `mark_matches_without_moves_as_corrupted`$$
CREATE PROCEDURE `mark_matches_without_moves_as_corrupted`()
BEGIN
    UPDATE Matches
    SET isCorrupted = 1
    WHERE id IN (
        SELECT m.id
        FROM Matches m
        LEFT JOIN Moves mv ON m.id = mv.matches_id
        WHERE mv.id IS NULL
    );
END$$
    WHERE mv.id IS NULL;
END$$

DROP PROCEDURE IF EXISTS `archive_old_matches`$$
CREATE PROCEDURE `archive_old_matches`(
    IN p_years INT
)
BEGIN
    START TRANSACTION;
        INSERT IGNORE INTO Matches_Archive
        SELECT *
        FROM Matches
        WHERE `timestamp` < DATE_SUB(NOW(), INTERVAL p_years YEAR);

        DELETE FROM Matches
        WHERE `timestamp` < DATE_SUB(NOW(), INTERVAL p_years YEAR);
    COMMIT;
END$$

DROP PROCEDURE IF EXISTS `recalc_games_played`$$
CREATE PROCEDURE `recalc_games_played`()
BEGIN
    UPDATE Players p
    SET games_played = (
        SELECT COUNT(*)
        FROM Matches m
        WHERE p.id IN (m.player_id_silver, m.player_id_gold)
    );
END$$

DROP PROCEDURE IF EXISTS `build_match_summary`$$
CREATE PROCEDURE `build_match_summary`()
BEGIN
    DELETE FROM match_summary;

    INSERT INTO match_summary (gametype, total_matches)
    SELECT gt.name, COUNT(*)
    FROM Matches m
    JOIN GameTypes gt ON gt.id = m.gameTypes_id
    GROUP BY gt.name;
END$$

DROP PROCEDURE IF EXISTS `move_corrupted_matches_to_archive`$$
CREATE PROCEDURE `move_corrupted_matches_to_archive`()
BEGIN
    START TRANSACTION;
        INSERT INTO Matches_Archive
        SELECT *
        FROM Matches
        WHERE isCorrupted = 1;

        DELETE FROM Matches
        WHERE isCorrupted = 1;
    COMMIT;
END$$

DELIMITER ;

-- Optional security setup (run manually when users exist):
-- GRANT EXECUTE ON PROCEDURE arimaadockermysqldb.get_event_participation TO 'report_user'@'%';
