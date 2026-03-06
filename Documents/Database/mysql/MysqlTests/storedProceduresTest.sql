
-- Test stored procedures (happy paths and edge cases)

-- Call each proc and verify outputs. Capture result sets and compare to 
-- expected values given the seed data.

-- Tips:
-- Record each result set and expected values 
-- in a small markdown or spreadsheet to demo pass/fail quickly.
-- If any procedure filters by dates, ensure your test matches 
-- include NOW() values so the rows are included.
    
    
    
-- 2.1 Totals
CALL get_total_players();        -- Expect 3
CALL get_total_matches();        -- Expect 3

-- 2.2 Player-centric stats
CALL get_matches_played_by_player(10);    -- Expect 2
CALL get_player_match_stats(10);          -- Check wins/losses align with seed rows
CALL find_players_without_rating();       -- Expect Carol (id 12)

-- 2.3 Leaderboards and breakdowns
CALL get_top_and_bottom_players();        -- Top=Alice(1700), Bottom=Bob(1600) if NULL excluded
CALL get_player_count_by_country();       -- Norway=2, USA=1

-- 2.4 GameType filters
-- If proc uses name, verify gametype exists by name 'Classic' or 'Blitz'
CALL get_player_matches_by_gametype(10, 'Classic');

-- 2.5 Event participation
CALL get_event_participation();           -- Events with and without matches

-- 2.6 Diagnostics
CALL find_matches_without_moves();        -- Should return none with current seed
CALL find_invalid_matches();              -- Should return none (self-play prevented by triggers)




