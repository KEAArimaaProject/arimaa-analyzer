# Arimaa Analyzer

A .NET MAUI application for analyzing Arimaa game records and board positions.

## Getting Started

### Prerequisites
- .NET 10 SDK installed

### Initial Setup

1. Navigate to the project directory:
```bash
cd ArimaaAnalyzer.Maui
```

2. Restore workloads:
```bash
dotnet workload restore
```

### Building & Running

For Windows:
```bash
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

## Database Seeding

Preliminary seeding can be done using the py package:
```bash
pip install "git+https://github.com/MaxusTheOne/arimaa_game_to_db.git"
```
or, with uv:
```bash
uv pip install "git+https://github.com/MaxusTheOne/arimaa_game_to_db.git"
```

Then, to seed the database, run:
```bash
arimaa-db-populate --database arimaadockermysqldb --games-file your/Path/to/game.txt
```
or, with uv:
```bash
uv run arimaa-db-populate --database arimaadockermysqldb --games-file your/Path/to/game.txt
```

Game path example: --games-file C:\Users\USER\RiderProjects\arimaa-analyzer\ArimaaAnalyzer.Maui\Resources\Raw\allgames202602.txt


flags:

--games-file: lokation to arimaa game file to use as seeding (required)

Database flags

--host | Default: localhost

--user | Default: root

--password | Default: 123456

--database | Default: mydb

--port | Default: 3307

