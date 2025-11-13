# RoslynBridge

> **Give AI assistants and external tools access to Visual Studio's Roslyn compiler**

RoslynBridge lets AI assistants like Claude Code understand your C# codebase by exposing Visual Studio's Roslyn API through a REST interface. Instead of just reading your code as text, AI can query diagnostics, explore symbols, find references, and understand your solution's semantic structure—just like Visual Studio does.

**Primary use case:** Enable AI-powered coding assistants to provide accurate, context-aware help with your C# projects.

## Why RoslynBridge?

When you ask an AI to help with C# code, it typically only sees your code as text. RoslynBridge gives AI tools access to:

- **Real compilation results** - See actual errors and warnings from Roslyn, not guesses
- **Semantic understanding** - Navigate symbols, find references, understand type relationships
- **Solution context** - Know which projects, files, and dependencies exist
- **Live feedback** - Query your currently-open Visual Studio instance in real-time

## Features

- Real-time diagnostics (errors, warnings, info)
- Symbol search and reference navigation
- Multi-instance Visual Studio support
- REST API + PowerShell helper script
- Built-in Claude Code integration
- No separate compilation required

## Installation

**Prerequisites:** Visual Studio 2022 (17.0+), .NET 8.0 SDK, Windows

### Quick Install

1. **Clone and build**
   ```bash
   git clone https://github.com/ajisaacs/RoslynBridge.git
   cd RoslynBridge
   "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" RoslynBridge.sln /t:Restore,Build /p:Configuration=Release
   ```

2. **Install the Visual Studio extension**
   ```bash
   # Double-click to install
   RoslynBridge\bin\Release\RoslynBridge.vsix
   ```
   Then restart Visual Studio.

3. **Install the WebAPI service** (requires Administrator)
   ```powershell
   scripts\webapi-install.ps1 -Configuration Release
   ```

4. **Verify it's working**
   ```bash
   curl http://localhost:5001/api/health/ping
   curl http://localhost:5001/api/instances
   ```

## Usage

### Using the REST API

Query the WebAPI using PowerShell or any HTTP client:

```powershell
# Get diagnostics summary
Invoke-RestMethod "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=MySolution"

# Get all errors
Invoke-RestMethod "http://localhost:5001/api/roslyn/diagnostics?solutionName=MySolution&severity=error"

# Find a symbol
Invoke-RestMethod "http://localhost:5001/api/roslyn/symbol/search?solutionName=MySolution&symbolName=MyClass"

# List all projects
Invoke-RestMethod "http://localhost:5001/api/roslyn/projects?solutionName=MySolution"
```

**Full API documentation:** Browse to `http://localhost:5001/swagger`

### Using the rb Helper Script (requires Git Bash)

If you have Git Bash installed, you can use the convenience script:

```bash
./scripts/rb summary    # Get diagnostics summary
./scripts/rb errors     # Get all errors
./scripts/rb warnings   # Get all warnings
./scripts/rb projects   # List projects
```

## Integration with Claude Code

RoslynBridge includes a Claude Code skill for AI-powered C# analysis.

**Setup:** Install Claude Code CLI. The skill auto-configures at `~/.claude/skills/roslyn-bridge`

**Usage:** Simply invoke the skill:
```
use skill roslyn-bridge
```

Claude can now check for errors, find symbol definitions, analyze diagnostics, and navigate your codebase.

## Troubleshooting

### No VS Instances Registered

`/api/instances` returns empty array

**Solutions:**
1. Ensure Visual Studio is running with a solution open
2. Check extension is installed: Extensions → Manage Extensions
3. Wait 60 seconds for initial registration
4. Check logs: View → Output → RoslynBridge

### Service Not Running

Connection refused to `localhost:5001`

**Solutions:**
```powershell
Get-Service RoslynBridgeWebApi    # Check status
Start-Service RoslynBridgeWebApi  # Start if stopped
scripts\webapi-install.ps1        # Reinstall if missing
```

### Wrong Solution Queried

Getting diagnostics from the wrong project

**Solutions:**
1. Check instances: `curl http://localhost:5001/api/instances`
2. Use exact `solutionName` from the response
3. Or use `rb` script from solution directory for auto-detection

## Service Management

```powershell
Start-Service RoslynBridgeWebApi    # Start
Stop-Service RoslynBridgeWebApi     # Stop
Restart-Service RoslynBridgeWebApi  # Restart
Get-Service RoslynBridgeWebApi      # Check status
```

## Reinstalling the VS Extension

```powershell
scripts\reinstall-vsix.ps1 -Configuration Release

# Options:
# -SkipBuild     Reuse existing VSIX
# -NoUninstall   Install over existing
```

## Additional Documentation

- [Architecture Details](ARCHITECTURE.md) - Technical design, flows, and diagrams
- [Contributing Guide](CONTRIBUTING.md) - Development setup and guidelines

## License

MIT License - see LICENSE file for details

---

**Made with ❤️ for the C# developer community**