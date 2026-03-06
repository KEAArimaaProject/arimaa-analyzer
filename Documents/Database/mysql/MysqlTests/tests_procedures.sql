-- Tests for stored procedures (readable outputs with labels)
USE `arimaadockermysqldb`;

SELECT '[PROC] get_total_players' AS label; CALL get_total_players();
SELECT '[PROC] get_total_matches' AS label; CALL get_total_matches();

SELECT '[PROC] get_matches_played_by_player (player 10)' AS label; CALL get_matches_played_by_player(10);
SELECT '[PROC] get_player_match_stats (player 10)' AS label; CALL get_player_match_stats(10);

SELECT '[PROC] find_players_without_rating' AS label; CALL find_players_without_rating();

SELECT '[PROC] get_top_and_bottom_players' AS label; CALL get_top_and_bottom_players();
SELECT '[PROC] get_player_count_by_country' AS label; CALL get_player_count_by_country();

SELECT '[PROC] get_player_matches_by_gametype (player 10, Classic)' AS label; CALL get_player_matches_by_gametype(10, 'Classic');

SELECT '[PROC] get_event_participation' AS label; CALL get_event_participation();

SELECT '[PROC] get_player_activity_by_month' AS label; CALL get_player_activity_by_month();

SELECT '[PROC] find_matches_without_moves' AS label; CALL find_matches_without_moves();
