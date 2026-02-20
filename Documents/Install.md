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
<AndroidSdkDirectory>C:\Users\hjlyk\AppData\Local\Android\Sdk</AndroidSdkDirectory>
```
## Project structure

- `ArimaaAnalyzer.Maui/` — Main application
  - `Components/` — Blazor components
  - `Services/` — Business logic and services
  - `Platforms/` — Platform-specific code
  - `Resources/` — Assets, icons, images
- `ArimaaAnalyzer.Tests/` — Test project
- `Documents/` — Documentation