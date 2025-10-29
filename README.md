# RoslynBridge

> **Bridge the gap between Visual Studio's Roslyn compiler platform and external tools**

RoslynBridge provides real-time C# code analysis capabilities by exposing Visual Studio's Roslyn API through a REST interface. It enables external tools, AI assistants (like Claude), and scripts to query diagnostics, explore symbols, analyze references, and interact with .NET solutions without requiring a full Visual Studio instance.

## Problem Statement

Programmatic access to C# code analysis typically requires either:
- Running `dotnet build` and parsing console output (slow, requires full compilation)
- Using standalone Roslyn APIs (complex setup, no Visual Studio context)
- Manual inspection in Visual Studio (not automatable)

RoslynBridge exposes Visual Studio's live Roslyn workspace via HTTP, enabling:
- Real-time access to diagnostics without compilation
- Symbol navigation and reference finding
- Integration with external tools and scripts
- Programmatic code analysis with full IDE context

## Architecture

```
┌─────────────────┐
│  External Tool  │  (Claude Code, Scripts, CI/CD)
│  (Port 5001)    │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────────────────────┐
│          RoslynBridge.WebApi                        │
│     (Windows Service - Port 5001)                   │
│  ┌──────────────────────────────────────────────┐   │
│  │  Instance Registry & Router                  │   │
│  │  - Tracks all VS instances                   │   │
│  │  - Routes by solution name                   │   │
│  │  - Manages heartbeats                        │   │
│  └──────────────────────────────────────────────┘   │
└────────┬─────────────────────────┬──────────────────┘
         │                         │
         ▼                         ▼
┌─────────────────┐       ┌─────────────────┐
│  VS Instance 1  │       │  VS Instance 2  │
│ (Port 59123)    │       │ (Port 59124)    │
│                 │       │                 │
│ RoslynBridge    │       │ CutFab          │
│ Extension       │       │ Extension       │
│ (VSIX)          │       │ (VSIX)          │
└─────────────────┘       └─────────────────┘
```

### Components

1. **RoslynBridge (Visual Studio Extension)**
   - VSIX package that loads when Visual Studio starts
   - Hosts HTTP server on dynamic port (59123, 59124, etc.)
   - Provides direct access to Roslyn workspace
   - Registers with WebAPI on startup
   - Sends heartbeats every 60 seconds

2. **RoslynBridge.WebApi (Windows Service)**
   - ASP.NET Core Web API running on port 5001
   - Maintains registry of all active VS instances
   - Routes requests to correct instance by solution name
   - Cleans up stale instances automatically
   - Provides unified API for multi-instance scenarios

## Features

### Code Analysis
- **Diagnostics**: Get errors, warnings, and info messages with file locations
- **Symbol Queries**: Find definitions, implementations, and references
- **Project Information**: List projects, files, and solution structure
- **Build Operations**: Trigger builds and add NuGet packages

### Multi-Instance Support
- Open multiple Visual Studio instances simultaneously
- Query specific solutions by name (no need to know port numbers)
- Automatic routing based on solution context
- Real-time instance health monitoring

### Developer Experience
- REST API with comprehensive endpoints
- PowerShell convenience script with auto-detection
- Swagger UI for API exploration
- Integration with Claude Code AI via skill

## Installation

### Prerequisites
- Visual Studio 2022 (version 17.0 or later)
- .NET 8.0 SDK
- Windows (for Windows Service support)

### Quick Install

1. **Clone the repository**
   ```bash
   git clone https://github.com/ajisaacs/RoslynBridge.git
   cd RoslynBridge
   ```

2. **Build the Visual Studio extension**
   ```bash
   "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" RoslynBridge.sln /t:Restore,Build /p:Configuration=Release
   ```

3. **Install the extension**
   - Locate `RoslynBridge\bin\Release\RoslynBridge.vsix`
   - Double-click to install in Visual Studio
   - Restart Visual Studio

4. **Install the WebAPI service** (requires Administrator)
   ```powershell
   # From repo root
   scripts\webapi-install.ps1 -Configuration Release
   ```

The service will start automatically and be available at `http://localhost:5001`

### Verify Installation

```bash
# Check service is running
curl http://localhost:5001/api/health/ping

# Check VS instances are registered
curl http://localhost:5001/api/instances
```

## Usage

### Quick Start with PowerShell Script

The easiest way to query your solution:

```powershell
# Navigate to your solution directory
cd C:\Projects\YourSolution

# Get diagnostics summary
.\scripts\webapi-query.ps1 summary

# Get all errors
.\scripts\webapi-query.ps1 errors

# Get all warnings
.\scripts\webapi-query.ps1 warnings

# List projects
.\scripts\webapi-query.ps1 projects
```

The script auto-detects your solution name from the current directory.

### REST API Examples

#### Get Diagnostics Summary
```bash
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=RoslynBridge"
```

**Response:**
```json
{
  "errorCount": 0,
  "warningCount": 2,
  "infoCount": 5,
  "hiddenCount": 390
}
```

#### Get All Errors
```bash
curl "http://localhost:5001/api/roslyn/diagnostics?solutionName=RoslynBridge&severity=error"
```

**Response:**
```json
{
  "success": true,
  "data": {
    "diagnostics": [
      {
        "id": "CS0103",
        "severity": "Error",
        "message": "The name 'undefined' does not exist in the current context",
        "filePath": "C:\\Projects\\RoslynBridge\\Program.cs",
        "startLine": 10,
        "startColumn": 5,
        "endLine": 10,
        "endColumn": 14
      }
    ]
  }
}
```

#### Find Symbol
```bash
curl "http://localhost:5001/api/roslyn/symbol/search?solutionName=RoslynBridge&symbolName=BridgeServer"
```

#### Get Solution Overview
```bash
curl "http://localhost:5001/api/roslyn/solution/overview?solutionName=RoslynBridge"
```

**Response:**
```json
{
  "success": true,
  "data": {
    "solutionName": "RoslynBridge",
    "projectCount": 2,
    "documentCount": 47,
    "projects": [
      {
        "name": "RoslynBridge",
        "documentCount": 30
      },
      {
        "name": "RoslynBridge.WebApi",
        "documentCount": 17
      }
    ]
  }
}
```

### Available API Endpoints

**Diagnostics:**
- `GET /api/roslyn/diagnostics/summary?solutionName=X` - Count by severity
- `GET /api/roslyn/diagnostics?solutionName=X` - All diagnostics
- `GET /api/roslyn/diagnostics?solutionName=X&severity=error` - Errors only
- `GET /api/roslyn/diagnostics?solutionName=X&severity=warning` - Warnings only

**Projects & Solution:**
- `GET /api/roslyn/projects?solutionName=X` - List all projects
- `GET /api/roslyn/solution/overview?solutionName=X` - Solution statistics

**Symbols:**
- `GET /api/roslyn/symbol/search?solutionName=X&symbolName=Y` - Find by name
- `GET /api/roslyn/symbol?solutionName=X&filePath=Z&line=N&column=M` - At location
- `GET /api/roslyn/references?solutionName=X&filePath=Z&line=N&column=M` - Find references

**Instance Management:**
- `GET /api/instances` - List all registered VS instances
- `GET /api/health` - Health check

**Full API documentation:** Browse to `http://localhost:5001/swagger` when the service is running

## Integration with Claude Code

RoslynBridge includes a Claude Code skill for AI-powered C# analysis.

### Setup

1. Install Claude Code CLI
2. The skill should be auto-configured at `~/.claude/skills/roslyn-bridge`

### Usage

Simply invoke the skill in Claude Code:
```
use skill roslyn-bridge
```

Claude can now:
- Check for compilation errors in your solution
- Find where symbols are defined or used
- Analyze diagnostics and suggest fixes
- Query project structure
- Navigate code relationships

## Configuration

### WebAPI Configuration (`appsettings.json`)

```json
{
  "Urls": "http://localhost:5001",
  "RoslynBridge": {
    "BaseUrl": "http://localhost:59123"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### VS Extension Configuration (`appsettings.json`)

```json
{
  "Server": {
    "Port": 0,
    "EnableAutoStart": true
  },
  "WebApi": {
    "BaseUrl": "http://localhost:5001",
    "RegistrationEndpoint": "/api/instances/register",
    "HeartbeatEndpoint": "/api/instances/heartbeat",
    "HeartbeatIntervalSeconds": 60
  }
}
```

## Service Management

```powershell
# Start the service
Start-Service RoslynBridgeWebApi

# Stop the service
Stop-Service RoslynBridgeWebApi

# Restart the service
Restart-Service RoslynBridgeWebApi

# Check service status
Get-Service RoslynBridgeWebApi

# View service logs
Get-EventLog -LogName Application -Source "RoslynBridge Web API" -Newest 50
```

## Troubleshooting

### No VS Instances Registered

**Symptoms:** `/api/instances` returns empty array

**Solutions:**
1. Verify Visual Studio is running with a solution open
2. Check RoslynBridge extension is installed (Extensions → Manage Extensions)
3. Wait 60 seconds for heartbeat to register
4. Check VS extension logs in Output window (View → Output → Show output from: RoslynBridge)

### Service Not Running

**Symptoms:** Connection refused to `localhost:5001`

**Solutions:**
```powershell
# Check service status
Get-Service RoslynBridgeWebApi

# Start if stopped
Start-Service RoslynBridgeWebApi

# Reinstall if missing
scripts\webapi-install.ps1 -Configuration Release
```

### Wrong Solution Queried

**Symptoms:** Getting diagnostics from wrong project

**Solutions:**
1. Check registered instances: `curl http://localhost:5001/api/instances`
2. Use exact `solutionName` from instances response
3. Use PowerShell script from solution directory for auto-detection

## Development

### Building from Source

```bash
# Build the extension
msbuild RoslynBridge\RoslynBridge.csproj /t:Build /p:Configuration=Debug

# Build the WebAPI
dotnet build RoslynBridge.WebApi\RoslynBridge.WebApi.csproj --configuration Debug

# Run WebAPI locally (without service)
dotnet run --project RoslynBridge.WebApi\RoslynBridge.WebApi.csproj
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test RoslynBridge.Tests\RoslynBridge.Tests.csproj
```

## Architecture Details

### Registration Flow

```
VS Starts → Load Extension → Start HTTP Server (59123)
                ↓
           POST /api/instances/register
                ↓
         WebAPI Registers Instance
                ↓
    Every 60s: POST /api/instances/heartbeat
                ↓
          WebAPI Updates Last Seen
                ↓
    Cleanup Service Removes Stale (>180s)
```

### Query Routing

```
External Request with ?solutionName=X
         ↓
    WebAPI Receives
         ↓
    Lookup in Registry by Solution Name
         ↓
    Forward to VS Instance Port (59123)
         ↓
    VS Extension Queries Roslyn Workspace
         ↓
    Return Results via WebAPI
```

## Use Cases

### CI/CD Integration
Query diagnostics during builds and fail if errors exist:
```bash
#!/bin/bash
errors=$(curl -s "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=MyApp" | jq '.errorCount')
if [ "$errors" -gt 0 ]; then
  echo "Build failed: $errors errors found"
  exit 1
fi
```

### Pre-commit Hooks
Check for errors before committing:
```bash
#!/bin/bash
errors=$(curl -s "http://localhost:5001/api/roslyn/diagnostics?solutionName=MyApp&severity=error")
if [ ! -z "$errors" ]; then
  echo "Cannot commit: compilation errors exist"
  exit 1
fi
```

### AI-Powered Code Review
Use Claude Code to analyze your codebase:
```
"Check this solution for potential bugs and code smells"
```
Claude will use RoslynBridge to get diagnostics, symbol information, and provide intelligent feedback.

## Performance

- **Initial connection**: ~50ms
- **Diagnostics query**: ~100-500ms (depends on solution size)
- **Symbol search**: ~50-200ms
- **Solution overview**: ~100ms

Queries use Visual Studio's in-memory Roslyn workspace, making them significantly faster than running `dotnet build`.

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

MIT License - see LICENSE file for details

## Credits

Built with:
- [Roslyn](https://github.com/dotnet/roslyn) - .NET Compiler Platform
- [Visual Studio SDK](https://docs.microsoft.com/en-us/visualstudio/extensibility/)
- [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)

## Support

- Report issues: [GitHub Issues](https://github.com/ajisaacs/RoslynBridge/issues)
- Documentation: See this README and inline code documentation

---

**Made with ❤️ for the C# developer community**
## Reinstalling the VS Extension

Use the helper script to build and reinstall the VSIX in Visual Studio 2022.

```powershell
# From repo root
scripts\reinstall-vsix.ps1 -Configuration Debug

# Options
# -SkipBuild     Reuse existing VSIX in bin/<Config>
# -NoUninstall   Skip uninstall step (install over existing)
# -VerboseOutput Show VSIXInstaller UI/log output
```

Quick double-click runner (passes args through):

```cmd
scripts\reinstall-vsix.cmd -Configuration Release
```

Requirements:
- Visual Studio 2022 with MSBuild and VSIXInstaller
- Execution policy allowing PowerShell scripts (or use the .cmd wrapper)



