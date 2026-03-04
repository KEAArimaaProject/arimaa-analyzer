# Installation and Running Arimaa Analyzer

This project is a .NET MAUI application that can run on Windows, macOS, and iOS. The project uses BlazorWebView to display web-based components.

## Prerequisites

### All platforms
- **.NET SDK 8.0 or later** (recommended: .NET 10.0)
  - Download from: https://dotnet.microsoft.com/download
  - Verify installation: `dotnet --version`

### Windows-specific requirements
- Visual Studio 2022 (recommended) with the "Desktop development with .NET" workload
- Or Visual Studio Code with the C# extension

### macOS-specific requirements
- Xcode -> Download from: https://developer.apple.com/xcode/ (or via App Store)
- Visual Studio for Mac or Visual Studio Code with the C# extension

### Linux
**Note:** .NET MAUI does not officially support Linux yet. The project can however be built as a .NET library (net8.0/net10.0) for testing purposes, but the UI cannot be run on Linux.

## Installation

### 1. Clone or download the project
```bash
git clone <repository-url>
cd arimaa-analyzer
```

### 2. Install .NET MAUI workloads
Before restoring packages, you need to install the required .NET MAUI workloads:

```bash
dotnet workload restore
```

This will automatically install the required workloads (such as `maui-android`, `maui-windows`, etc.) based on what the project needs.

**Alternative:** If `dotnet workload restore` doesn't work, you can install workloads manually:
```bash
# Install all MAUI workloads
dotnet workload install maui

# Or install specific workloads
dotnet workload install maui-android
dotnet workload install maui-windows
dotnet workload install maui-ios
dotnet workload install maui-maccatalyst
```

### 3. Restore NuGet packages
```bash
dotnet restore
```

## Running the project

### Windows

#### Method 1: Via dotnet CLI (recommended)
```powershell
# Navigate to the project folder
cd ArimaaAnalyzer.Maui

# Run on Windows
dotnet run --framework net10.0-windows10.0.19041.0
```

#### Method 2: Via Visual Studio
1. Open `arimaa-analyzer.sln` in Visual Studio 2022
2. Set "ArimaaAnalyzer.Maui" as the startup project
3. Select platform: "Windows Machine" or "net10.0-windows10.0.19041.0"
4. Press **F5** or click "Start"

### macOS

**Note:** To run on macOS (Mac Catalyst or iOS), you need Xcode installed.

#### Mac Catalyst (recommended for macOS desktop)
```bash
# Navigate to the project folder
cd ArimaaAnalyzer.Maui

# Run on Mac Catalyst
dotnet run --framework net10.0-maccatalyst
```

#### iOS Simulator (if Xcode is installed)
```bash
# Run on iOS simulator
dotnet build -t:Run -f net10.0-ios
```

#### Via Visual Studio for Mac or Visual Studio Code
1. Open `arimaa-analyzer.sln`
2. Set "ArimaaAnalyzer.Maui" as the startup project
3. Select platform: "Mac Catalyst" or "iOS"
4. Press **F5** or click "Start"

### Linux

**Important:** .NET MAUI does not officially support Linux. The project can however be built as a testable library:

```bash
# Build as net8.0 library (services only, no UI)
dotnet build ArimaaAnalyzer.Maui/ArimaaAnalyzer.Maui.csproj -f net8.0

# Or as net10.0 library
dotnet build ArimaaAnalyzer.Maui/ArimaaAnalyzer.Maui.csproj -f net10.0
```

This will only include the service layer (from the `Services/` folder) and can be used to test business logic, but the UI cannot be run.

**Alternatives for Linux:**
- Use Windows Subsystem for Linux (WSL) with a Windows build
- Run the project in a Windows/macOS VM
- Wait for official Linux support from Microsoft


### Testing the project
The project also includes a test project (`ArimaaAnalyzer.Tests`):

```bash
# Run all tests
dotnet test

# Run tests for a specific project
dotnet test ArimaaAnalyzer.Tests/ArimaaAnalyzer.Tests.csproj
```

## Troubleshooting

### "To build this project, the following workloads must be installed: maui-android"
This error occurs when MAUI workloads are not installed. Run:
```bash
dotnet workload restore
```

Or install MAUI workloads manually:
```bash
dotnet workload install maui
```

### "Target framework not found"
Ensure you have the correct .NET SDK installed:
```bash
dotnet --list-sdks
```

### "NuGet packages not restored"
Run the restore command:
```bash
dotnet restore
```

### "Android SDK not found" (only relevant for Android builds)
The project has a hardcoded Android SDK path in the `.csproj` file. Update it if needed:
```xml
<AndroidSdkDirectory>C:\Users\USER\AppData\Local\Android\Sdk</AndroidSdkDirectory>
```

# Through Database-specific instructions


## Database setup (Docker + MySQL)

This project includes a ready-to-run MySQL database using Docker. It will auto-seed the schema on the first run using the SQL in `Documents/Database/mysql/Sql_arimaa_init.sql`.

### What’s included

- `Documents/Database/docker-compose.yml`
  - Uses a stable MySQL image tag: `mysql:8.4`
  - Exposes MySQL on your host at port `3307`
  - Persists data in a named Docker volume `mysql_data`
  - Mounts `./mysql` into `/docker-entrypoint-initdb.d` so the `.sql` file(s) inside are executed automatically the FIRST time a brand-new data volume is created
- `Documents/Database/mysql/Sql_arimaa_init.sql`
  - Creates the `arimaadockermysqldb` schema and all necessary tables
  - Uses `CREATE ... IF NOT EXISTS` for safe re-runs

### Prerequisites

- Docker Desktop installed and running
- PowerShell (commands below assume Windows; adapt for macOS/Linux shells if needed)

### Start the database (auto-seeding)

1. Open a terminal in the database folder:
   ```powershell
   cd C:\Users\USER\PROJECTFOLDER\arimaa-analyzer\Documents\Database
   ```
2. Start MySQL via Docker Compose (detached):
   ```powershell
   docker compose up -d
   ```
   - If your environment only has legacy Compose v1, use:
     ```powershell
     docker-compose up -d
     ```
3. Watch logs until the server is ready and seeding is complete (first run only):
   ```powershell
   docker logs -f arimaadockermysqldb
   ```
   You should see MySQL startup messages and (on a brand-new volume) execution of files from `/docker-entrypoint-initdb.d`, including `Sql_arimaa_init.sql`.

3a. If any errors occur, and you need to revert 
    the changes you made with docker-compose up -d,
    then you can do this:


#### Stop stack and remove the data volume
     docker compose down -v

#### (Optional) Remove any leftover container with the same name
      docker rm -f arimaadockermysqldb 2>$null

#### (Optional) Remove the Compose network if it still exists
#### docker network ls | findstr arimaa-analyzer

#### (Optional) If you want to force a fresh image pull next start
#### docker image rm mysql:9.5.0

#### Start fresh; since the volume is new, init scripts will run
     docker compose up -d


### Verify the database and tables

Run a quick check using the MySQL client inside the container:
```powershell
docker exec -it arimaadockermysqldb mysql -uroot -p123456 -e "SHOW DATABASES; USE arimaadockermysqldb; SHOW TABLES;"
```
You should see `arimaadockermysqldb` and a list of tables (e.g., `Puzzles`, `Countries`, `Players`, etc.).

### Connection details (for apps/tools)

- Host: `localhost`
- Port: `3307`
- User: `root`
- Password: `123456`
- Default database: `arimaadockermysqldb`

Example .NET connection string:
```text
Server=localhost;Port=3307;Database=arimaadockermysqldb;User ID=root;Password=123456;SslMode=None;
```

### Re-seeding from scratch (apply schema changes)

The auto-seeding only runs when the data volume is created. If you modify `Sql_arimaa_init.sql` and want a fresh seed:
```powershell
cd C:\Users\USER\PROJECTFOLDER\arimaa-analyzer\Documents\Database
docker compose down -v   # removes containers and the mysql_data volume
docker compose up -d     # starts MySQL and re-runs init scripts
```

### Optional: Run the SQL manually using container-only commands

If you don’t want to rely on the initial auto-seed, you can import the SQL using only Docker (no MySQL client on the host needed):

- Pipe the SQL into the containerized client (PowerShell):
  ```powershell
  cd C:\Users\USER\PROJECTFOLDER\arimaa-analyzer\Documents\Database
  Get-Content .\mysql\Sql_arimaa_init.sql | docker exec -i arimaadockermysqldb mysql -uroot -p123456
  ```

- Or copy the file into the container and execute it there:
  ```powershell
  cd C:\Users\USER\PROJECTFOLDER\arimaa-analyzer\Documents\Database
  docker cp .\mysql\Sql_arimaa_init.sql arimaadockermysqldb:/tmp/init.sql
  docker exec -it arimaadockermysqldb mysql -uroot -p123456 -e "SOURCE /tmp/init.sql"
  ```

### Stopping and removing

- Stop (but keep data):
  ```powershell
  docker compose down
  ```
- Stop and remove data volume (use with care):
  ```powershell
  docker compose down -v
  ```

### Troubleshooting

- Image tag fails to pull or start
  - The compose file uses `mysql:8.4`, a stable MySQL LTS tag. If pulling fails, ensure Docker Desktop is running and you have network access.

- Port conflict on `3307`
  - Edit `Documents/Database/docker-compose.yml` and change the left side of the port mapping, e.g. `3308:3306`. Then reconnect using that port.

- Seeding did not run
  - Auto-seeding only occurs for a brand-new data volume. To force re-seed, run `docker compose down -v` and start again.

- Compose v1 vs v2
  - Preferred: `docker compose ...` (v2, integrated with Docker Desktop)
  - Legacy: `docker-compose ...` (v1). Use only if v2 is unavailable.

### Summary of what’s configured

- MySQL container: `arimaadockermysqldb`
- Image: `mysql:8.4`
- Exposed port: host `3307` -> container `3306`
- Root user with password `123456`
- Auto-seeding from `Documents/Database/mysql/Sql_arimaa_init.sql` on first startup with a new volume