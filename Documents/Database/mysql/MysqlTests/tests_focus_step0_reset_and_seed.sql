-- Step 0: Reset schema/objects and seed using official seed_data.sql for focused tests
-- This prepares the database so subsequent ACTION steps can be executed one-by-one.

SOURCE Documents/Database/mysql/MysqlTests/reset_and_build.sql;

-- Use the project's comprehensive seed data as the initial seed
SOURCE Documents/Database/mysql/MysqlTests/seed_data.sql;

USE `arimaadockermysqldb`;

-- Ensure required GameType id=83 exists for the demo match insert used in later steps
INSERT IGNORE INTO GameTypes (id, name, time_increment, time_reserve)
VALUES (83, 'Custom 83', NULL, NULL);

