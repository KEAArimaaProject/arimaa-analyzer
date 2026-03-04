@echo off
setlocal

set "PROJECT_DIR=%~dp0ArimaaAnalyzer.Maui"
set "COMPOSE_DIR=%~dp0Documents\Database"
set "TFM=net10.0-windows10.0.19041.0"

if /I "%~1"=="install" goto :install
if /I "%~1"=="run" goto :run

:usage
echo Usage:
echo   %~n0 install  ^(restore workloads + restore packages^)
echo   %~n0 run      ^(start docker compose + run app for %TFM%^)
exit /b 1

:install
pushd "%PROJECT_DIR%" || exit /b 1
dotnet workload restore || (popd & exit /b 1)
dotnet restore || (popd & exit /b 1)
popd
exit /b 0

:run
pushd "%COMPOSE_DIR%" || exit /b 1
docker compose up -d || (popd & exit /b 1)
popd

pushd "%PROJECT_DIR%" || exit /b 1
dotnet run -f %TFM% || (popd & exit /b 1)
popd
exit /b 0
