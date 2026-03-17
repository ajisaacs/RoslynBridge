# Claude Code Development Notes

## RoslynBridge MCP Server

This project includes an MCP (Model Context Protocol) server that exposes Roslyn code analysis tools to Claude. The MCP server replaces the previous skill-based approach.

### MCP Configuration

The MCP server is configured in `.mcp.json` at the project root:

```json
{
  "mcpServers": {
    "roslyn-bridge": {
      "command": "dotnet",
      "args": ["run", "--project", "./RoslynBridge.Mcp/RoslynBridge.Mcp.csproj", "--no-build"]
    }
  }
}
```

### Available MCP Tools (32 total)

**Diagnostics (3):**
- `get_diagnostics` - Get compiler errors and warnings
- `get_diagnostics_summary` - Get counts by severity
- `get_diagnostics_count` - Get total diagnostic count

**Symbols (13):**
- `get_symbol` - Get symbol info at a position
- `find_references` - Find all references to a symbol
- `get_references_count` - Count references
- `search_symbol` - Search symbols by name
- `get_type_members` - Get members of a type
- `get_type_hierarchy` - Get inheritance hierarchy
- `find_implementations` - Find interface implementations
- `get_call_hierarchy` - Get callers/callees
- `search_code` - Regex pattern search
- `get_symbol_context` - Get symbol context info
- `get_namespace_types` - Get types in a namespace
- `get_symbol_source` - Get source code of a symbol by name
- `find_usages` - Find files referencing a symbol (by name, returns file paths grouped by project)

**Projects (8):**
- `get_projects` - List all projects
- `get_files` - Get files with filtering
- `get_solution_overview` - Get solution statistics
- `build_project` - Build a project
- `clean_project` - Clean build outputs
- `restore_packages` - Restore NuGet packages
- `add_package` - Add a NuGet package
- `remove_package` - Remove a NuGet package

**Code Quality (3):**
- `get_code_smells` - Detect code smells
- `get_code_smell_summary` - Summary by smell type
- `get_duplicates` - Find duplicate code

**Instances (3):**
- `list_instances` - List VS instances
- `get_instance_by_solution` - Find instance by solution
- `check_health` - Health check

**Workspace (2):**
- `refresh_workspace` - Reload files from disk
- `format_document` - Format a file

### Prerequisites

1. **Visual Studio** with RoslynBridge extension installed
2. **RoslynBridge.WebApi** running on port 5001
3. **Solution open** in Visual Studio

### Building the MCP Server

```bash
dotnet build RoslynBridge.Mcp -c Release
```

### Configuration

The MCP server connects to the WebAPI at `http://localhost:5001` by default. This can be changed in `RoslynBridge.Mcp/appsettings.json`.
