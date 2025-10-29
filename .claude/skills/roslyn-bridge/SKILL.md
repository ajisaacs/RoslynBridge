---
name: roslyn-bridge
description: Use this for C# code analysis, querying .NET projects, finding symbols, getting diagnostics, or any Roslyn/semantic analysis tasks via the WebAPI gateway.
---

# Roslyn Bridge - WebAPI Guide

All queries go through the WebAPI. Do not call the VSIX port directly. The WebAPI auto-routes to the correct VS instance by solution name, keeps history, and returns normalized responses.

## Critical Rules

- Always check health and instances first
- Always include `solutionName` in queries (except `/api/instances`)
- Never guess — query the API for facts

## Quick Start

```bash
# List registered VS instances
curl http://localhost:5001/api/instances

# Diagnostics summary for a solution
curl "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=RoslynBridge"
```

## Script (recommended)

- Script: `scripts/rb.ps1`
- Auto-detects solution from the current directory (or use `-SolutionName`).

Examples:
- `./scripts/rb.ps1 summary`
- `./scripts/rb.ps1 projects -SolutionName "RoslynBridge"`
- `./scripts/rb.ps1 symbolAt -FilePath "C:\\repo\\Foo.cs" -Line 10 -Column 5`
- `./scripts/rb.ps1 typemembers -SymbolName "MyNamespace.MyClass" -IncludeInherited`
- `./scripts/rb.ps1 addpkg -ProjectName App -PackageName Newtonsoft.Json -Version 13.0.3`

## Core Endpoints

- Base: `http://localhost:5001`
- Instances: `GET /api/instances`
- Health: `GET /api/health`, `GET /api/health/ping`
- Roslyn (convenience):
  - `GET /api/roslyn/projects?solutionName=X`
  - `GET /api/roslyn/solution/overview?solutionName=X`
  - `GET /api/roslyn/diagnostics?solutionName=X[&severity=error]`
  - `GET /api/roslyn/diagnostics/summary?solutionName=X`
  - `GET /api/roslyn/symbol?solutionName=X&filePath=Z&line=N&column=M`
  - `GET /api/roslyn/references?solutionName=X&filePath=Z&line=N&column=M`
  - `GET /api/roslyn/symbol/search?solutionName=X&symbolName=Y`
  - `POST /api/roslyn/project/package/add?solutionName=X&projectName=Y&packageName=Z[&version=V]`
  - `POST /api/roslyn/project/build?solutionName=X&projectName=Y[&configuration=Debug]`
- Generic pass-through for anything else: `POST /api/roslyn/query?solutionName=X` with body `{ queryType, ... }`

## Query Types (for POST /api/roslyn/query)

- Discovery
  - `getprojects`
  - `getdocument` — `filePath`
  - `getsolutionoverview`
  - `getsemanticmodel` — `filePath`
  - `getsyntaxtree` — `filePath`
  - `getnamespacetypes` — `symbolName` (namespace)
  - `searchcode` — `symbolName` (regex), `parameters.scope` = `all|methods|classes|properties`

- Symbols
  - `getsymbol` — `filePath`, `line`, `column`
  - `findsymbol` — `symbolName`, optional `parameters.kind`
  - `gettypemembers` — `symbolName`, optional `parameters.includeInherited`
  - `gettypehierarchy` — `symbolName`, optional `parameters.direction` = `up|down|both`
  - `findimplementations` — `symbolName` OR (`filePath`,`line`,`column`)
  - `getcallhierarchy` — `filePath`,`line`,`column`, optional `parameters.direction` (default `callers`)
  - `getsymbolcontext` — `filePath`,`line`,`column`

- Diagnostics
  - `getdiagnostics` — optional `filePath`
  - `findreferences` — `filePath`,`line`,`column`

- Refactoring
  - `formatdocument` — `filePath`
  - `organizeusings` — `filePath`
  - `renamesymbol` — `filePath`,`line`,`column`, `parameters.newName`
  - `addmissingusing` — `filePath`,`line`,`column`
  - `applycodefix` — `filePath`,`line`,`column`

- Project Operations
  - `addnugetpackage` — `projectName`,`packageName`, optional `version`
  - `removenugetpackage` — `projectName`,`packageName`
  - `buildproject` — `projectName`, optional `configuration`
  - `cleanproject` — `projectName`
  - `restorepackages` — `projectName`
  - `createdirectory` — `directoryPath`

## Notes

- Lines are 1-based; columns are 0-based
- Use absolute file paths; escape backslashes in JSON
- Response shape: `{ success, message, data, error }`

## Troubleshooting

- Empty `/api/instances`:
  - VS not running, extension not installed, or heartbeat not yet sent
- 400/405 errors:
  - Check method (GET vs POST). Generic queries use POST `/api/roslyn/query`.
  - Ensure `solutionName` is provided where required
