---
name: roslyn-bridge
description: Use this for C# code analysis, querying .NET projects, finding symbols, getting diagnostics, or any Roslyn/semantic analysis tasks using the bridge server
---

# Roslyn Bridge Testing Guide

Use this guide when testing or accessing the Claude Roslyn Bridge HTTP endpoints.

## Server Info
- **Base URL**: `http://localhost:59123`
- **Endpoints**:
  - `/query` - Main query endpoint for all Roslyn operations
  - `/health` - Health check endpoint to verify server is running
- **Method**: POST
- **Content-Type**: application/json

## ⚠️ CRITICAL: Command Syntax Rules

**ALWAYS use curl with the Bash tool. NEVER pipe curl output to PowerShell.**

### ✅ CORRECT: Using curl with Bash tool

```bash
# Test if server is running
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getprojects\"}"

# Get diagnostics
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getdiagnostics\"}"

# Get document info
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getdocument\",\"filePath\":\"C:\\\\path\\\\to\\\\file.cs\"}"

# Get symbol at position
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getsymbol\",\"filePath\":\"C:\\\\Users\\\\AJ\\\\Desktop\\\\PepLib\\\\PepLib\\\\Program.cs\",\"line\":10,\"column\":5}"
```

**IMPORTANT curl syntax rules:**
- Use `-X POST` (NOT `-Method POST`)
- Use `-H` for headers (NOT `-Headers`)
- Use `-d` for data (NOT `-Body`)
- Escape backslashes in file paths: `\\\\` becomes `\\` in JSON
- **DO NOT pipe to PowerShell** - the JSON output is already formatted

### ❌ WRONG: DO NOT DO THIS

```bash
# NEVER pipe curl to PowerShell - this will fail on Windows
curl ... | powershell -Command "$json = $input | ..."  # ❌ WRONG
```

### Using PowerShell (Alternative)

Run these commands **directly in PowerShell**, NOT via `powershell -Command`:

```powershell
# Test server
$body = @{queryType='getprojects'} | ConvertTo-Json
Invoke-RestMethod -Uri 'http://localhost:59123/query' -Method Post -Body $body -ContentType 'application/json'

# Get document
$body = @{
    queryType='getdocument'
    filePath='C:\path\to\file.cs'
} | ConvertTo-Json
Invoke-RestMethod -Uri 'http://localhost:59123/query' -Method Post -Body $body -ContentType 'application/json'
```

**IMPORTANT PowerShell rules:**
- Run directly in PowerShell console, NOT via `bash -c` or `powershell -Command`
- Use single quotes around URI and content type
- File paths don't need escaping in PowerShell hash tables

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

## Common Test Examples

```bash
# Health check - verify server is running
curl -X POST http://localhost:59123/health -H "Content-Type: application/json" -d "{}"

# Get all projects in solution
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getprojects\"}"

# Get solution overview
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getsolutionoverview\"}"

# Get diagnostics for entire solution
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getdiagnostics\"}"

# Get semantic model for a file
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getsemanticmodel\",\"filePath\":\"C:\\\\path\\\\to\\\\file.cs\"}"

# Get syntax tree for a file
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getsyntaxtree\",\"filePath\":\"C:\\\\path\\\\to\\\\file.cs\"}"

# Find all classes containing "Helper"
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"searchcode\",\"symbolName\":\".*Helper\",\"parameters\":{\"scope\":\"classes\"}}"

# Format a document
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"formatdocument\",\"filePath\":\"C:\\\\path\\\\to\\\\file.cs\"}"

# Add NuGet package
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"addnugetpackage\",\"projectName\":\"RoslynBridge\",\"packageName\":\"Newtonsoft.Json\"}"

# Add NuGet package with version
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"addnugetpackage\",\"projectName\":\"RoslynBridge\",\"packageName\":\"Newtonsoft.Json\",\"version\":\"13.0.3\"}"

# Remove NuGet package
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"removenugetpackage\",\"projectName\":\"RoslynBridge\",\"packageName\":\"Newtonsoft.Json\"}"

# Build project
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"buildproject\",\"projectName\":\"RoslynBridge\"}"

# Build with configuration
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"buildproject\",\"projectName\":\"RoslynBridge\",\"configuration\":\"Release\"}"

# Clean project
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"cleanproject\",\"projectName\":\"RoslynBridge\"}"

# Restore packages
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"restorepackages\",\"projectName\":\"RoslynBridge\"}"

# Create directory
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"createdirectory\",\"directoryPath\":\"C:\\\\path\\\\to\\\\new\\\\directory\"}"
```

## Response Format

Success:
```json
{
  "success": true,
  "message": "Optional message",
  "data": { /* Response data */ }
}
```

Error:
```json
{
  "success": false,
  "error": "Error message",
  "data": null
}
```

## Notes

- Line numbers are **1-based**
- Column numbers are **0-based**
- File paths in JSON need escaped backslashes: `C:\\path\\to\\file.cs`
- All workspace modifications use VS threading model (`JoinableTaskFactory.SwitchToMainThreadAsync()`)
- The Visual Studio extension must be running for endpoints to work
