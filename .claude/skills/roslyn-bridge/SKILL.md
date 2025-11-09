---
name: roslyn-bridge
description: Specialized interface for C# and .NET projects. **AUTOMATICALLY ACTIVATE THIS SKILL at the start of ANY C#/.NET project work** (detecting .sln, .csproj, or C# file operations). Use for code analysis, symbol/reference lookup, diagnostics, code quality analysis (code smells, duplicates, refactoring opportunities), or build actions requiring semantic accuracy through Roslyn. CRITICAL: Always use this for C# projects instead of text-based search tools like Grep/Glob.
---

# Roslyn Bridge - C# Code Analysis

## AUTO-ACTIVATION TRIGGERS

**This skill should be AUTOMATICALLY ACTIVATED when:**
- Working in a directory containing .sln or .csproj files
- User requests any C# or .NET code analysis, search, or modification
- About to use Grep/Glob on C# files (.cs extension)
- User asks about errors, warnings, diagnostics, or build issues
- User asks to find/search for C# symbols, classes, methods, or properties

**Once activated, this skill's instructions remain active for the entire conversation.**

## When to Use This Skill

**ALWAYS use Roslyn Bridge when:**
- Finding code smells or refactoring opportunities
- Analyzing code quality or detecting duplicates
- Searching for C# symbols, classes, methods, or properties
- Finding references or implementations
- Checking diagnostics, errors, or warnings
- Getting type hierarchies or call graphs
- Building or managing projects/packages

**NEVER use text-based search (Grep/Glob) for C# code** - Roslyn provides semantic analysis that's faster and more accurate.

## Usage Directive
ALWAYS use Roslyn Bridge for all code searches, references, symbol lookups, and diagnostics.
Never fall back to regex/grep unless the bridge explicitly fails.

## Reference Documentation
When detailed API specifications or curl fallback examples are needed, load these references:
- `references/api_endpoints.md` - Complete API endpoint specifications (grep: "GET /api/roslyn")
- `references/curl_fallback.md` - Direct curl examples when rb unavailable (grep: "curl http")

## CRITICAL RULES
1. **ALWAYS USE rb FIRST** - Primary method for all code searches, references, and symbol lookups.
2. **Use curl as fallback** - Only if rb doesn't work
3. **ALWAYS CHECK INSTANCES FIRST** - Verify VS is running and get solution name
4. **NEVER GUESS** - Always query the API for actual information

## 🎯 FINDING SYMBOLS: WHICH COMMAND TO USE?

**PRIMARY: Use `symbol --name` for finding types, classes, interfaces, methods, properties:**
```bash
# ✅ BEST: Find any symbol by name (most reliable)
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" symbol --name Material
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" symbol --name IRepository
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" symbol --name CalculateTotal

# OR use the convenient aliases:
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" find-class Material
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" find-type IRepository
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" find-symbol CalculateTotal
```

**Returns:**
- Symbol location (file path, line number)
- Symbol kind (class, interface, method, property, etc.)
- All references across the solution
- Full metadata (namespace, accessibility, modifiers)

**ADVANCED: Use `searchcode` for complex pattern matching within code:**
```bash
# Pattern matching for code structures (regex)
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" searchcode "async.*Task" --scope methods
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" searchcode "DbContext" --scope classes

# ⚠️ Note: searchcode is for REGEX PATTERNS, NOT simple name lookups
# If searching for "class Material", use: rb symbol --name Material
```

**Rule of thumb:**
- **Need to find a class/method/property by name?** → `symbol --name ClassName`
- **Need to find code patterns with regex?** → `searchcode "pattern.*regex"`


## Quick Start - Use rb Helper Script

**The rb bash script is available in two locations:**
- Project scripts directory: `./scripts/rb`
- Skill scripts directory: `"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb"`

Both auto-detect the solution from the current directory.

```bash
# Step 1: Check what VS instances are running
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" instances

# Step 2: Use rb commands (auto-detects solution from current directory)
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" overview
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" summary
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

**Basic Diagnostics:**
```bash
# Get diagnostics summary (START HERE)
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" summary

# Get all errors
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" errors

# Get all warnings
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" warnings

# Get all diagnostics with details
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" diagnostics

# Get solution overview/statistics
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" overview

# List all projects
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" projects

# Check instances
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" instances

# Health check
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" health
```

**Code Quality Analysis:**
```bash
# Get code smells
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" codesmells --top 10 --severity-filter High

# Find duplicate code
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" duplicates --min-lines 5 --similarity 80
```

**Symbol Queries:**
```bash
# Find symbol by name (BEST for finding classes/methods/properties)
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" symbol --name Drawing

# Get symbol at specific location
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" symbolAt --file 'C:\Path\To\File.cs' --line 10 --column 5

# Find all references
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" refs --file 'C:\Path\To\File.cs' --line 10 --column 5

# Get type members
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" typemembers --name MyNamespace.MyClass

# Get type hierarchy
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" typehierarchy --name MyNamespace.MyClass --direction both

# Find implementations
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" implementations --name IMyInterface

# Call hierarchy
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" callhierarchy --file 'C:\Path\To\File.cs' --line 10 --column 5 --call-dir callers
```

**Advanced Queries:**
```bash
# Search code with regex patterns (for complex pattern matching)
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" searchcode "async.*Task" --scope methods
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" searchcode "DbContext" --scope classes
```

**Project Operations:**
```bash
# Build project
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" build --project CutFab.Web

# Add NuGet package
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" addpkg --project CutFab.Web --package Newtonsoft.Json --version 13.0.3

# Clean project
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" clean --project CutFab.Web

# Restore packages
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" restore --project CutFab.Web
```

**Override Solution Name (if needed):**
```bash
# Use specific solution instead of auto-detect
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" summary --solution 'CustomSolution'
```

## Workflow Pattern

**Follow this order for best results:**

```bash
# 1. Check instances to verify VS is running
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" instances

# 2. Get solution overview
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" overview

# 3. Get diagnostics summary
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" summary

# 4. If errors exist, get details
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" errors

# 5. For code quality analysis, check for code smells
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" codesmells --top 10 --severity-filter High

# 6. If you need file paths, get projects list
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" projects

# 7. Query specific symbols as needed (BEST for finding classes/methods/properties)
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" symbol --name ClassName
```

## Fallback: Direct curl API Calls

**Only use these if the rb script is not available or not working.**

For detailed curl command examples and troubleshooting, see `references/curl_fallback.md`.

Quick health check:
```bash
curl http://localhost:5001/api/health
curl http://localhost:5001/api/instances
```

## API Endpoints Reference

For complete API endpoint documentation with all parameters and examples, see `references/api_endpoints.md`.

## Important Notes

- **Line numbers are 1-based** (first line = 1)
- **Column numbers are 0-based** (first column = 0)
- **File paths must be absolute** - Get them from projects list
- **rb script auto-detects solution** from .sln file in current directory or parent directories
- **Use backslashes for Windows paths** in rb script: `C:\Path\To\File.cs`
- **Use forward slashes in curl URLs**: `C:/Path/To/File.cs`

## Troubleshooting

### rb Script Not Found
```bash
# Check if it exists in the scripts directory
ls scripts/rb

# Or check in the skill directory
ls "$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb"
```

### No Instances Found
```bash
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" instances
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
"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb" summary --solution 'CorrectSolutionName'
```

## DO

- ✅ Always use rb script first for all queries: `"$USERPROFILE/.claude/skills/roslyn-bridge/scripts/rb"`
- ✅ **Use `symbol --name ClassName`** to find classes, interfaces, methods, properties (most reliable)
- ✅ Use `searchcode` only for regex pattern matching, NOT simple name lookups
- ✅ Always check instances first with rb instances command
- ✅ Use curl as fallback only if rb script doesn't work
- ✅ Get actual diagnostics from API - never guess
- ✅ Use full absolute file paths from projects list
- ✅ Let rb script auto-detect solution when possible

## DO NOT

- ❌ Try to analyze C# code manually
- ❌ Guess about errors, warnings, or file locations
- ❌ Use relative file paths
- ❌ Skip checking instances first
- ❌ Query VS plugin directly (port 59123+) - always use WebAPI
- ❌ Use curl when rb script is available and working
