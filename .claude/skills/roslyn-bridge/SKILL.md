---
name: roslyn-bridge
description: Use this for C# code analysis, querying .NET projects, finding symbols, getting diagnostics, or any Roslyn/semantic analysis tasks using the bridge server
---

# Roslyn Bridge API Guide

Use this guide when accessing the Roslyn Bridge for C# code analysis.

## Architecture

```
┌─────────────┐      REST API       ┌────────────────────┐      HTTP      ┌─────────────────────┐
│   Claude    │ ◄─────────────────► │  Web API (:5000)   │ ◄────────────► │  VS Plugin (:59123) │
│     AI      │                     │    Middleware      │                │   Roslyn Bridge     │
└─────────────┘                     └────────────────────┘                └─────────────────────┘
```

## Recommended: Web API (Port 5000)

**Base URL**: `http://localhost:5000`

The Web API provides a modern RESTful interface with:
- Clean REST endpoints with query parameters
- Full Swagger documentation at `/`
- Request/response history tracking at `/api/history`
- CORS support for web applications
- Better error handling and logging

### Web API Endpoints

**Health & Info:**
- `GET /api/health` - Check health of both Web API and VS plugin
- `GET /api/health/ping` - Simple ping

**Roslyn Operations:**
- `GET /api/roslyn/projects` - Get all projects
- `GET /api/roslyn/solution/overview` - Solution statistics
- `GET /api/roslyn/diagnostics?filePath={path}` - Get errors/warnings
- `GET /api/roslyn/symbol?filePath={path}&line={line}&column={col}` - Get symbol info
- `GET /api/roslyn/references?filePath={path}&line={line}&column={col}` - Find references
- `GET /api/roslyn/symbol/search?symbolName={name}&kind={kind}` - Search symbols
- `POST /api/roslyn/query` - Execute any query (fallback to raw queries)
- `POST /api/roslyn/format` - Format document
- `POST /api/roslyn/project/package/add` - Add NuGet package
- `POST /api/roslyn/project/build` - Build project

**History:**
- `GET /api/history` - All history entries
- `GET /api/history/{id}` - Specific entry
- `GET /api/history/recent?count=50` - Recent entries
- `GET /api/history/stats` - Statistics
- `DELETE /api/history` - Clear history

## Alternative: Direct VS Plugin Access (Port 59123)

**Base URL**: `http://localhost:59123`

Direct access to the Visual Studio plugin (use only if Web API is unavailable):
- **Endpoints**: `/query`, `/health`
- **Method**: POST only
- **Content-Type**: application/json

## ⚠️ CRITICAL: Command Syntax Rules

**ALWAYS use curl with the Bash tool. NEVER pipe curl output to PowerShell.**

### ✅ CORRECT: Using Web API (Recommended)

```bash
# Health check
curl http://localhost:5000/api/health

# Get all projects (returns full file paths)
curl http://localhost:5000/api/roslyn/projects

# Get solution overview
curl http://localhost:5000/api/roslyn/solution/overview

# Get diagnostics for entire solution
curl http://localhost:5000/api/roslyn/diagnostics

# Get diagnostics for specific file
curl "http://localhost:5000/api/roslyn/diagnostics?filePath=C:/path/to/file.cs"

# Get symbol at position (use paths from /projects response)
curl "http://localhost:5000/api/roslyn/symbol?filePath=C:/path/to/file.cs&line=10&column=5"

# Find all references
curl "http://localhost:5000/api/roslyn/references?filePath=C:/path/to/file.cs&line=18&column=30"

# Search for symbols
curl "http://localhost:5000/api/roslyn/symbol/search?symbolName=MyClass&kind=class"

# Get recent history
curl http://localhost:5000/api/history/recent?count=10

# Get history statistics
curl http://localhost:5000/api/history/stats
```

**IMPORTANT curl syntax rules:**
- For GET requests, no `-X GET` needed
- Use quotes around URLs with query parameters
- Forward slashes `/` work in file paths (Windows accepts both `/` and `\`)
- **DO NOT pipe to PowerShell** - the JSON output is already formatted

### Using Direct VS Plugin Access (Fallback)

```bash
# Health check - verify VS plugin is running
curl -X POST http://localhost:59123/health -H "Content-Type: application/json" -d "{}"

# Get projects via POST
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getprojects\"}"

# Get symbol at position
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getsymbol\",\"filePath\":\"C:\\\\path\\\\to\\\\file.cs\",\"line\":10,\"column\":5}"
```

**Note:** In POST requests to VS plugin, escape backslashes: `\\\\` becomes `\\` in JSON

## Quick Reference: All Endpoints

### Query Endpoints

| Endpoint | Required Fields | Optional Fields | Description |
|----------|----------------|-----------------|-------------|
| `getprojects` | - | - | Get all projects in the solution |
| `getdocument` | `filePath` | - | Get document information for a specific file |
| `getsymbol` | `filePath`, `line`, `column` | - | Get symbol information at a specific position |
| `getsemanticmodel` | `filePath` | - | Verify semantic model availability (not serializable) |
| `getsyntaxtree` | `filePath` | - | Get the syntax tree (source code) for a file |
| `getdiagnostics` | - | `filePath` | Get compilation errors and warnings |
| `findreferences` | `filePath`, `line`, `column` | - | Find all references to a symbol |
| `findsymbol` | `symbolName` | `parameters.kind` | Find symbols by name (supports filtering by kind) |
| `gettypemembers` | `symbolName` | `parameters.includeInherited` | Get all members of a type |
| `gettypehierarchy` | `symbolName` | `parameters.direction` | Get base types or derived types |
| `findimplementations` | `symbolName` OR `filePath`+`line`+`column` | - | Find implementations of an interface/abstract member |
| `getnamespacetypes` | `symbolName` | - | Get all types in a namespace |
| `getcallhierarchy` | `filePath`, `line`, `column` | `parameters.direction` | Get callers or callees of a method |
| `getsolutionoverview` | - | - | Get high-level solution statistics |
| `getsymbolcontext` | `filePath`, `line`, `column` | - | Get contextual information about a symbol's location |
| `searchcode` | `symbolName` (regex) | `parameters.scope` | Search for code patterns using regex |

### Editing Endpoints

| Endpoint | Required Fields | Optional Fields | Description |
|----------|----------------|-----------------|-------------|
| `formatdocument` | `filePath` | - | Format a document according to coding style |
| `organizeusings` | `filePath` | - | Sort and remove unused using statements |
| `renamesymbol` | `filePath`, `line`, `column`, `parameters.newName` | - | Rename a symbol across the solution |
| `addmissingusing` | `filePath`, `line`, `column` | - | Add missing using statement for a symbol |
| `applycodefix` | `filePath`, `line`, `column` | - | Apply available code fixes at a position |

### Project Operation Endpoints

| Endpoint | Required Fields | Optional Fields | Description |
|----------|----------------|-----------------|-------------|
| `addnugetpackage` | `projectName`, `packageName` | `version` | Add a NuGet package to a project |
| `removenugetpackage` | `projectName`, `packageName` | - | Remove a NuGet package from a project |
| `buildproject` | `projectName` | `configuration` | Build a project or solution |
| `cleanproject` | `projectName` | - | Clean build output |
| `restorepackages` | `projectName` | - | Restore NuGet packages |
| `createdirectory` | `directoryPath` | - | Create a new directory |

## Common Workflow Examples

### Typical Code Analysis Workflow

```bash
# Step 1: Check if services are healthy
curl http://localhost:5000/api/health

# Step 2: Get all projects and their files
curl http://localhost:5000/api/roslyn/projects
# Response includes: {"data": [{"documents": ["C:/Full/Path/To/File.cs", ...]}]}

# Step 3: Use the full paths from Step 2 in subsequent queries
FILE="C:/Users/AJ/Desktop/MyProject/Program.cs"

# Get diagnostics (errors/warnings) for a file
curl "http://localhost:5000/api/roslyn/diagnostics?filePath=$FILE"

# Get symbol information at a specific location
curl "http://localhost:5000/api/roslyn/symbol?filePath=$FILE&line=15&column=10"

# Find all references to that symbol
curl "http://localhost:5000/api/roslyn/references?filePath=$FILE&line=15&column=10"

# Step 4: View your query history
curl http://localhost:5000/api/history/recent?count=5

# Step 5: Get statistics about your queries
curl http://localhost:5000/api/history/stats
```

### Advanced Query Examples

```bash
# Search for all classes with "Service" in the name
curl "http://localhost:5000/api/roslyn/symbol/search?symbolName=Service&kind=class"

# Get solution-wide statistics
curl http://localhost:5000/api/roslyn/solution/overview

# For complex queries not available as REST endpoints, use POST /query
curl -X POST http://localhost:5000/api/roslyn/query \
  -H "Content-Type: application/json" \
  -d '{"queryType":"searchcode","symbolName":".*Controller","parameters":{"scope":"classes"}}'

# Build a project
curl -X POST "http://localhost:5000/api/roslyn/project/build?projectName=MyProject"

# Add NuGet package
curl -X POST "http://localhost:5000/api/roslyn/project/package/add?projectName=MyProject&packageName=Newtonsoft.Json&version=13.0.3"
```

## Response Format

All endpoints return JSON in this format:

**Success:**
```json
{
  "success": true,
  "message": "Optional message",
  "data": { /* Response data varies by endpoint */ },
  "error": null
}
```

**Error:**
```json
{
  "success": false,
  "message": null,
  "data": null,
  "error": "Error description"
}
```

## Important Notes

- Line numbers are **1-based** (first line is 1)
- Column numbers are **0-based** (first column is 0)
- **Use full paths from `/api/roslyn/projects` response** in other endpoints
- Forward slashes `/` work in file paths (Windows accepts both `/` and `\`)
- Both the Web API (port 5000) and VS Plugin (port 59123) must be running
- All workspace modifications use VS threading model
- Request history is automatically tracked in the Web API

## Troubleshooting

**"Failed to connect" error:**
1. Check if Web API is running: `curl http://localhost:5000/api/health/ping`
2. Check if VS Plugin is running: `curl -X POST http://localhost:59123/health -H "Content-Type: application/json" -d "{}"`
3. Ensure Visual Studio is open with a solution loaded

**"vsPluginStatus": "Disconnected":**
- The VS Plugin (port 59123) is not running
- Open Visual Studio and ensure the RoslynBridge extension is loaded

**Empty or null data:**
- Ensure you're using the correct file paths from `/api/roslyn/projects`
- Verify line/column numbers are within the file bounds
- Check that the file is part of the currently open VS solution
