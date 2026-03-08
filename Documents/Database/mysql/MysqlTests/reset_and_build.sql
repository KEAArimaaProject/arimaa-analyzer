-- Reset schema and build all DB objects from existing scripts
-- Run via: mysql ... < run_all_tests.sql (which SOURCES this file)

-- Drop existing schema to ensure a clean start for tests
DROP SCHEMA IF EXISTS `arimaadockermysqldb`;

-- Build base schema and objects (source existing repo files)
SOURCE Documents/Database/mysql/Sql_arimaa_10_init.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_20_support.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_30_procedures.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_40_triggers.sql;
SOURCE Documents/Database/mysql/Sql_arimaa_50_events.sql;

-- Ensure we are using the expected schema for subsequent scripts
USE `arimaadockermysqldb`;
