---
name: roslyn-bridge
description: Use this for C# code analysis, querying .NET projects, finding symbols, getting diagnostics, or any Roslyn/semantic analysis tasks using the bridge server
---

# Roslyn Bridge - C# Code Analysis

## CRITICAL RULES

1. **ALWAYS USE THIS TOOL FIRST** - Never try to analyze C# code manually
2. **ALWAYS CHECK HEALTH FIRST** - Run health check before any queries
3. **ALWAYS USE solutionName PARAMETER** - Route to correct VS instance
4. **NEVER GUESS** - Always query the API for actual information

## Quick Start

```bash
# Step 1: Check what's running
curl http://localhost:5001/api/instances

# Step 2: Query with solution name
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=RoslynBridge"
```

## Architecture

```
Claude → WebAPI (:5001) → Correct VS Instance (auto-routed by solutionName)
```

- WebAPI runs as Windows Service on port 5001
- Multiple VS instances can be open (different ports: 59123, 59124, etc.)
- WebAPI routes based on `solutionName` parameter

## Primary Method: Use query.ps1 Script

**Location:** `C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1`

**Auto-detects solution from current directory - NO solutionName needed!**

```powershell
# Navigate to solution dir first
cd C:\Users\AJ\Desktop\RoslynBridge

# Then run commands (auto-detects RoslynBridge)
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" summary
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" errors
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" warnings
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" projects
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" overview
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" instances
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" health

# Override if needed
powershell -ExecutionPolicy Bypass -File "C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge.WebApi\query.ps1" summary -SolutionName "CutFab"
```

**Available Commands:**
- `summary` - Diagnostics count by severity (START HERE)
- `errors` - All errors
- `warnings` - All warnings
- `diagnostics` - All diagnostics with details
- `projects` - List projects
- `overview` - Solution stats
- `instances` - VS instances
- `health` - Health check

## Fallback Method: curl with solutionName

**ALWAYS include `?solutionName=XXX` parameter!**

### Most Common Queries

```bash
# 1. Check what VS instances are registered
curl http://localhost:5001/api/instances

# 2. Get diagnostics summary (errors/warnings count)
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=RoslynBridge"

# 3. Get all errors only
curl "http://localhost:5001/api/roslyn/diagnostics?solutionName=RoslynBridge&severity=error"

# 4. Get all warnings only
curl "http://localhost:5001/api/roslyn/diagnostics?solutionName=RoslynBridge&severity=warning"

# 5. Get all diagnostics with details
curl "http://localhost:5001/api/roslyn/diagnostics?solutionName=RoslynBridge"

# 6. List all projects
curl "http://localhost:5001/api/roslyn/projects?solutionName=RoslynBridge"

# 7. Get solution overview
curl "http://localhost:5001/api/roslyn/solution/overview?solutionName=RoslynBridge"
```

### Symbol Queries

```bash
# Find symbol by name
curl "http://localhost:5001/api/roslyn/symbol/search?solutionName=RoslynBridge&symbolName=ClassName"

# Get symbol at specific location
curl "http://localhost:5001/api/roslyn/symbol?solutionName=RoslynBridge&filePath=C:/Full/Path/File.cs&line=10&column=5"

# Find all references
curl "http://localhost:5001/api/roslyn/references?solutionName=RoslynBridge&filePath=C:/Full/Path/File.cs&line=10&column=5"
```

### Multi-Solution

```bash
# Query RoslynBridge
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=RoslynBridge"

# Query CutFab
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=CutFab"
```

## All API Endpoints

**All support `?solutionName=XXX` parameter**

### Diagnostics
- `/api/roslyn/diagnostics/summary?solutionName=X` - Count by severity
- `/api/roslyn/diagnostics?solutionName=X` - All diagnostics
- `/api/roslyn/diagnostics?solutionName=X&severity=error` - Only errors
- `/api/roslyn/diagnostics?solutionName=X&severity=warning` - Only warnings
- `/api/roslyn/diagnostics?solutionName=X&filePath=C:/path/file.cs` - File-specific

### Projects & Solution
- `/api/roslyn/projects?solutionName=X` - All projects with file paths
- `/api/roslyn/solution/overview?solutionName=X` - Solution statistics

### Symbols
- `/api/roslyn/symbol/search?solutionName=X&symbolName=Y` - Find symbol by name
- `/api/roslyn/symbol?solutionName=X&filePath=Z&line=N&column=M` - Symbol at location
- `/api/roslyn/references?solutionName=X&filePath=Z&line=N&column=M` - Find references

### Project Operations
- `POST /api/roslyn/project/build?solutionName=X&projectName=Y` - Build project
- `POST /api/roslyn/project/package/add?solutionName=X&projectName=Y&packageName=Z` - Add package

### Instances & Health
- `/api/instances` - List all VS instances with solutions
- `/api/health` - Health check

## Workflow Pattern

**ALWAYS follow this order:**

```bash
# 1. Check instances
curl http://localhost:5001/api/instances

# 2. Get diagnostics summary
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=RoslynBridge"

# 3. If errors exist, get details
curl "http://localhost:5001/api/roslyn/diagnostics?solutionName=RoslynBridge&severity=error"

# 4. If need file paths, get projects
curl "http://localhost:5001/api/roslyn/projects?solutionName=RoslynBridge"

# 5. Query specific symbols/references as needed
curl "http://localhost:5001/api/roslyn/symbol/search?solutionName=RoslynBridge&symbolName=X"
```

## Important Notes

- Line numbers are 1-based (first line = 1)
- Column numbers are 0-based (first column = 0)
- File paths must be absolute/full paths
- Get file paths from `/api/roslyn/projects` response
- WebAPI service: `Start-Service RoslynBridgeWebApi` / `Stop-Service RoslynBridgeWebApi`

## Troubleshooting

### No Instances
```bash
curl http://localhost:5001/api/instances
```
If empty:
- Visual Studio not running
- RoslynBridge extension not installed
- No solution open
- Wait 60 seconds for heartbeat

### Wrong Solution
Always check instances and use correct solutionName:
```bash
curl http://localhost:5001/api/instances
# Use exact solutionName from response
```

### Service Not Running
```bash
curl http://localhost:5001/api/health/ping
```
If fails:
- Service not running: `Start-Service RoslynBridgeWebApi`
- Check service status: `Get-Service RoslynBridgeWebApi`

## DO NOT

- ❌ Try to analyze C# code manually
- ❌ Guess about errors or warnings
- ❌ Use file paths without querying projects first
- ❌ Omit solutionName parameter (except /api/instances)
- ❌ Query VS plugin directly (port 59123) - use WebAPI

## DO

- ✅ Always check /api/instances first
- ✅ Always use solutionName parameter
- ✅ Use query.ps1 when possible (auto-detects solution)
- ✅ Get actual diagnostics from API
- ✅ Use full file paths from /projects response
