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

Then, to seed the database, run:
```bash
arimaa-db-populate --database arimaadockermysqldb --games-file your/Path/to/game.txt
```
flags:

--games-file: lokation to arimaa game file to use as seeding (required)

Database flags

--host

--user

--password

--database

--port

