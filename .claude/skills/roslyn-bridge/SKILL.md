---
name: roslyn-bridge
description: Specialized interface for C# and .NET projects. Use this skill for any code
  analysis, symbol or reference lookup, diagnostics, code quality analysis (code smells,
  duplicate detection), project queries, or build actions that require semantic accuracy
  through Roslyn. This replaces all generic text, grep, or regex searches for C# code -
  always prefer this when working with .NET solutions or Visual Studio projects.
---

# Roslyn Bridge - C# Code Analysis


## Usage Directive
ALWAYS use Roslyn Bridge for all code searches, references, symbol lookups, and diagnostics.
Never fall back to regex/grep unless the bridge explicitly fails.

## CRITICAL RULES
1. **ALWAYS USE rb FIRST** - Primary method for all code searches, references, and symbol lookups.
2. **Use curl as fallback** - Only if rb doesn't work
3. **ALWAYS CHECK INSTANCES FIRST** - Verify VS is running and get solution name
4. **NEVER GUESS** - Always query the API for actual information

## ⚡ NEW: searchcode Simplified Syntax
The `searchcode` command now accepts the pattern as a positional argument:
```bash
# PREFERRED: Positional argument (simple and clean)
~/.claude/skills/roslyn-bridge/scripts/rb searchcode "MyClassName"
~/.claude/skills/roslyn-bridge/scripts/rb searchcode "async.*Task" --scope methods

# ALSO WORKS: --pattern flag
~/.claude/skills/roslyn-bridge/scripts/rb searchcode --pattern "MyClassName"
```


## Quick Start - Use rb Helper Script

**The rb bash script is available in two locations:**
- Project scripts directory: `./scripts/rb`
- Skill scripts directory: `~/.claude/skills/roslyn-bridge/scripts/rb`

Both auto-detect the solution from the current directory.

```bash
# Step 1: Check what VS instances are running
~/.claude/skills/roslyn-bridge/scripts/rb instances

# Step 2: Use rb commands (auto-detects solution from current directory)
~/.claude/skills/roslyn-bridge/scripts/rb overview
~/.claude/skills/roslyn-bridge/scripts/rb summary
```

## Architecture

```
Claude → rb → WebAPI (:5001) → VS Instance (auto-routed by solution)
```

- rb auto-detects solution name from current directory
- WebAPI runs as Windows Service on port 5001
- Multiple VS instances can be open simultaneously
- WebAPI routes requests based on solution name

## Most Common rb Commands

**NOTE:** The examples below may show old PowerShell syntax. The current version uses the bash script `~/.claude/skills/roslyn-bridge/scripts/rb`. Replace `powershell -Command "& ./scripts/rb.ps1 command -Param value"` with `~/.claude/skills/roslyn-bridge/scripts/rb command --param value`.

**Basic Diagnostics:**
```bash
# Get diagnostics summary (START HERE)
powershell -Command "& ./scripts/rb.ps1 summary"

# Get all errors
powershell -Command "& ./scripts/rb.ps1 errors"

# Get all warnings
powershell -Command "& ./scripts/rb.ps1 warnings"

# Get all diagnostics with details
powershell -Command "& ./scripts/rb.ps1 diagnostics"

# Get solution overview/statistics
powershell -Command "& ./scripts/rb.ps1 overview"

# List all projects
powershell -Command "& ./scripts/rb.ps1 projects"

# Check instances
powershell -Command "& ./scripts/rb.ps1 instances"

# Health check
powershell -Command "& ./scripts/rb.ps1 health"
```

**Code Quality Analysis:**
```bash
# Get code smells
powershell -Command "& ./scripts/rb.ps1 codesmells -Top 10 -SeverityFilter High"

# Get code smells summary
powershell -Command "& ./scripts/rb.ps1 codesmellsummary"

# Find duplicate code
powershell -Command "& ./scripts/rb.ps1 duplicates -MinLines 5 -Similarity 80"
```

**Symbol Queries:**
```bash
# Find symbol by name
powershell -Command "& ./scripts/rb.ps1 symbol -SymbolName Drawing"

# Get symbol at specific location
powershell -Command "& ./scripts/rb.ps1 symbolAt -FilePath 'C:\Path\To\File.cs' -Line 10 -Column 5"

# Find all references
powershell -Command "& ./scripts/rb.ps1 refs -FilePath 'C:\Path\To\File.cs' -Line 10 -Column 5"

# Get type members
powershell -Command "& ./scripts/rb.ps1 typemembers -SymbolName MyNamespace.MyClass"

# Get type hierarchy
powershell -Command "& ./scripts/rb.ps1 typehierarchy -SymbolName MyNamespace.MyClass -HierarchyDirection both"

# Find implementations
powershell -Command "& ./scripts/rb.ps1 implementations -SymbolName IMyInterface"

# Call hierarchy
powershell -Command "& ./scripts/rb.ps1 callhierarchy -FilePath 'C:\Path\To\File.cs' -Line 10 -Column 5 -CallDirection callers"
```

**Advanced Queries:**
```bash
# Search code with regex (PREFERRED: positional argument)
~/.claude/skills/roslyn-bridge/scripts/rb searchcode "async.*Task" --scope methods

# Search code with regex (alternative: --pattern flag)
~/.claude/skills/roslyn-bridge/scripts/rb searchcode --pattern "DbContext" --scope classes

# Simple search
~/.claude/skills/roslyn-bridge/scripts/rb searchcode "MyClassName"
```

**Project Operations:**
```bash
# Build project
powershell -Command "& ./scripts/rb.ps1 build -ProjectName CutFab.Web"

# Add NuGet package
powershell -Command "& ./scripts/rb.ps1 addpkg -ProjectName CutFab.Web -PackageName Newtonsoft.Json -Version 13.0.3"

# Clean project
powershell -Command "& ./scripts/rb.ps1 clean -ProjectName CutFab.Web"

# Restore packages
powershell -Command "& ./scripts/rb.ps1 restore -ProjectName CutFab.Web"
```

**Override Solution Name (if needed):**
```bash
# Use specific solution instead of auto-detect
powershell -Command "& ./scripts/rb.ps1 summary -SolutionName 'CustomSolution'"
```

## Workflow Pattern

**Follow this order for best results:**

```bash
# 1. Check instances to verify VS is running
powershell -Command "& ./scripts/rb.ps1 instances"

# 2. Get solution overview
powershell -Command "& ./scripts/rb.ps1 overview"

# 3. Get diagnostics summary
powershell -Command "& ./scripts/rb.ps1 summary"

# 4. If errors exist, get details
powershell -Command "& ./scripts/rb.ps1 errors"

# 5. If you need file paths, get projects list
powershell -Command "& ./scripts/rb.ps1 projects"

# 6. Query specific symbols as needed
powershell -Command "& ./scripts/rb.ps1 symbol -SymbolName ClassName"
```

## Fallback: Direct curl API Calls

**Only use these if rb.ps1 is not available or not working.**

Replace `CutFab` with your solution name:

```bash
# Check instances
curl http://localhost:5001/api/instances

# Get diagnostics summary
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=CutFab"

# Get all errors only
curl "http://localhost:5001/api/roslyn/diagnostics?solutionName=CutFab&severity=error"

# List all projects
curl "http://localhost:5001/api/roslyn/projects?solutionName=CutFab"

# Get solution overview
curl "http://localhost:5001/api/roslyn/solution/overview?solutionName=CutFab"

# Find symbol by name
curl "http://localhost:5001/api/roslyn/symbol/search?solutionName=CutFab&symbolName=Drawing"

# Get symbol at specific location
curl "http://localhost:5001/api/roslyn/symbol?solutionName=CutFab&filePath=C:/Full/Path/File.cs&line=10&column=5"

# Find all references
curl "http://localhost:5001/api/roslyn/references?solutionName=CutFab&filePath=C:/Full/Path/File.cs&line=10&column=5"
```

## All API Endpoints Reference

### Health & Instances
- `GET /api/health` - Health check (no params)
- `GET /api/instances` - List all VS instances (no params)

### Diagnostics
- `GET /api/roslyn/diagnostics/summary?solutionName=X` - Count by severity
- `GET /api/roslyn/diagnostics?solutionName=X` - All diagnostics
- `GET /api/roslyn/diagnostics?solutionName=X&severity=error` - Only errors
- `GET /api/roslyn/diagnostics?solutionName=X&severity=warning` - Only warnings

### Projects & Solution
- `GET /api/roslyn/projects?solutionName=X` - All projects with file paths
- `GET /api/roslyn/solution/overview?solutionName=X` - Solution statistics

### Symbols
- `GET /api/roslyn/symbol/search?solutionName=X&symbolName=Y` - Find symbol by name
- `GET /api/roslyn/symbol?solutionName=X&filePath=Z&line=N&column=M` - Symbol at location
- `GET /api/roslyn/references?solutionName=X&filePath=Z&line=N&column=M` - Find references

### Project Operations
- `POST /api/roslyn/project/build?solutionName=X&projectName=Y` - Build project
- `POST /api/roslyn/project/package/add?solutionName=X&projectName=Y&packageName=Z&version=V` - Add package

## Important Notes

- **Line numbers are 1-based** (first line = 1)
- **Column numbers are 0-based** (first column = 0)
- **File paths must be absolute** - Get them from projects list
- **rb.ps1 auto-detects solution** from .sln file in current directory or parent directories
- **Use backslashes for Windows paths** in rb.ps1: `C:\Path\To\File.cs`
- **Use forward slashes in curl URLs**: `C:/Path/To/File.cs`

## Troubleshooting

### rb.ps1 Not Found
```bash
# Check if it exists in the scripts directory
ls scripts/rb.ps1

# Or check in the skill directory
ls .\.claude\skills\roslyn-bridge\scripts\rb.ps1
```

### No Instances Found
```bash
powershell -Command "& ./scripts/rb.ps1 instances"
# Returns: []
```
**Causes:**
- Visual Studio not running
- RoslynBridge extension not installed
- No solution open in VS
- Wait 60 seconds for heartbeat registration

### Service Connection Issues
```bash
curl http://localhost:5001/api/health
# Connection refused or timeout
```
**Note:** The RoslynBridge WebAPI service runs in the background and should always be available on port 5001. If you get connection errors, the service may need to be restarted (contact system admin).

### Wrong Solution Detected
```bash
# Override with specific solution name
powershell -Command "& ./scripts/rb.ps1 summary -SolutionName 'CorrectSolutionName'"
```

## DO

- ✅ Always use `powershell -Command "& ./scripts/rb.ps1"` first for all queries
- ✅ Always check instances first with `powershell -Command "& ./scripts/rb.ps1 instances"`
- ✅ Use curl as fallback only if rb.ps1 doesn't work
- ✅ Get actual diagnostics from API - never guess
- ✅ Use full absolute file paths from projects list
- ✅ Let rb.ps1 auto-detect solution when possible

## DO NOT

- ❌ Try to analyze C# code manually
- ❌ Guess about errors, warnings, or file locations
- ❌ Use relative file paths
- ❌ Skip checking instances first
- ❌ Query VS plugin directly (port 59123+) - always use WebAPI
- ❌ Use curl when rb.ps1 is available and working
