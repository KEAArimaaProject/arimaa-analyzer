-- Triggers
-- Requires: Sql_arimaa_init.sql, Sql_arimaa_20_support.sql

USE `mydb`;

DELIMITER $$

DROP TRIGGER IF EXISTS `trg_players_update_audit`$$
CREATE TRIGGER `trg_players_update_audit`
AFTER UPDATE ON Players
FOR EACH ROW
BEGIN
    INSERT INTO audit_log (
        table_name,
        operation,
        record_id,
        old_value,
        new_value
    )
    VALUES (
        'Players',
        'UPDATE',
        OLD.id,
        JSON_OBJECT(
            'username', OLD.username,
            'rating', OLD.rating,
            'country', OLD.countries_id
        ),
        JSON_OBJECT(
            'username', NEW.username,
            'rating', NEW.rating,
            'country', NEW.countries_id
        )
    );
END$$

DROP TRIGGER IF EXISTS `trg_match_insert_update_games`$$
CREATE TRIGGER `trg_match_insert_update_games`
AFTER INSERT ON Matches
FOR EACH ROW
BEGIN
    UPDATE Players
    SET games_played = IFNULL(games_played, 0) + 1
    WHERE id IN (NEW.player_id_silver, NEW.player_id_gold);
END$$

DROP TRIGGER IF EXISTS `trg_prevent_self_play`$$
CREATE TRIGGER `trg_prevent_self_play`
BEFORE INSERT ON Matches
FOR EACH ROW
BEGIN
    IF NEW.player_id_silver = NEW.player_id_gold THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'A player cannot play against themselves';
    END IF;
END$$

DROP TRIGGER IF EXISTS `trg_set_match_time`$$
CREATE TRIGGER `trg_set_match_time`
BEFORE INSERT ON Matches
FOR EACH ROW
BEGIN
    IF NEW.`timestamp` IS NULL THEN
        SET NEW.`timestamp` = CURRENT_TIMESTAMP;
    END IF;
END$$

DELIMITER ;
