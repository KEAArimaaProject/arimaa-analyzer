

@echo off
REM Create db in a virtual environment. Cd into /database before running in command propmpt

docker-compose up -d
timeout /t 10
mysql --host=localhost --port=3307 --user=root --password=123456 < Sql_arimaa_init.sql
echo Database setup completed successfully!
@REM docker compose down to remove environment
pause