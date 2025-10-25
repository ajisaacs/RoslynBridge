---
name: roslyn-api
description: Use this for C# code analysis, querying .NET projects, finding symbols, getting diagnostics, or any Roslyn/semantic analysis tasks using the bridge server
---

# Roslyn API Testing Guide

Use this guide when testing or accessing the Claude Roslyn Bridge HTTP endpoints.

## Server Info
- **Base URL**: `http://localhost:59123/query`
- **Method**: POST
- **Content-Type**: application/json

## Correct Command Syntax

### Using curl (Recommended on Windows)

```bash
# Test if server is running
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getprojects\"}"

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

| Endpoint | Required Fields | Optional Fields |
|----------|----------------|-----------------|
| `getprojects` | - | - |
| `getdocument` | `filePath` | - |
| `getsymbol` | `filePath`, `line`, `column` | - |
| `getdiagnostics` | - | `filePath` |
| `findreferences` | `filePath`, `line`, `column` | - |
| `findsymbol` | `symbolName` | `parameters.kind` |
| `gettypemembers` | `symbolName` | `parameters.includeInherited` |
| `gettypehierarchy` | `symbolName` | `parameters.direction` |
| `findimplementations` | `symbolName` OR `filePath`+`line`+`column` | - |
| `getnamespacetypes` | `symbolName` | - |
| `getcallhierarchy` | `filePath`, `line`, `column` | `parameters.direction` |
| `getsolutionoverview` | - | - |
| `getsymbolcontext` | `filePath`, `line`, `column` | - |
| `searchcode` | `symbolName` (regex) | `parameters.scope` |

### Editing Endpoints

| Endpoint | Required Fields | Optional Fields |
|----------|----------------|-----------------|
| `formatdocument` | `filePath` | - |
| `organizeusings` | `filePath` | - |
| `renamesymbol` | `filePath`, `line`, `column`, `parameters.newName` | - |
| `addmissingusing` | `filePath`, `line`, `column` | - |
| `applycodefix` | `filePath`, `line`, `column` | - |

## Common Test Examples

```bash
# Get all projects in solution
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getprojects\"}"

# Get solution overview
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getsolutionoverview\"}"

# Get diagnostics for entire solution
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"getdiagnostics\"}"

# Find all classes containing "Helper"
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"searchcode\",\"symbolName\":\".*Helper\",\"parameters\":{\"scope\":\"classes\"}}"

# Format a document
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d "{\"queryType\":\"formatdocument\",\"filePath\":\"C:\\\\path\\\\to\\\\file.cs\"}"
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
