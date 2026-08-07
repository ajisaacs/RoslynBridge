# Design: get_symbol_source, Enum Values, find_usages

**Date:** 2026-03-17
**Status:** Approved

## Overview

Three new capabilities for RoslynBridge that reduce reliance on file `Read` calls during code analysis:

1. **`get_symbol_source`** — retrieve the actual source code of a symbol by name
2. **Enum values in `get_type_members`** — include constant values for enum members
3. **`find_usages`** — lightweight file-path-only reference finder by symbol name

## Feature 1: `get_symbol_source`

### Purpose

`get_type_members` returns signatures but not implementation code. To see a method body, you currently need to `Read` the file. This tool returns the full source text of a symbol's declaration.

### MCP Tool Interface

```
Tool: get_symbol_source
Inputs:
  - symbolName (string, required): Fully or partially qualified name, e.g. "MyClass.MyMethod" or "MyClass"
  - solutionName (string, optional): Target solution
Returns:
  - For each matching declaration:
    - filePath: absolute path to the source file
    - startLine / endLine: 1-based line range
    - source: full source text of the declaration node
```

### Behavior

- **Method/constructor/operator:** Full declaration including body braces and content
- **Property:** Declaration with accessors (get/set bodies)
- **Type (class/struct/interface/record):** Full type declaration including all members
- **Enum:** Full enum declaration with all values
- **Field/event:** The field/event declaration statement
- If multiple symbols match the name, all are returned (each with file path and line range)
- **Partial classes:** Each partial declaration is returned separately with its own file path. The tool description notes that partial types may span multiple files.
- **Matching strategy:** Uses exact name match first (case-insensitive). If no exact match, falls back to ends-with match (e.g., "MyMethod" matches "MyNamespace.MyClass.MyMethod"). This avoids substring noise (searching "Get" won't match every getter).
- **Source text:** Uses `node.ToString()` (without leading/trailing trivia) to avoid stray whitespace and doc comments from adjacent declarations. Leading XML doc comments for the symbol itself are prepended separately.

### VS Extension Implementation

New method in `SymbolQueryService`:

```csharp
public async Task<QueryResponse> GetSymbolSourceAsync(QueryRequest request)
```

1. Find symbol(s) by name using `compilation.GetSymbolsWithName()` with exact match, falling back to ends-with
2. For each symbol, get its `DeclaringSyntaxReferences`
3. Get the syntax node via `syntaxReference.GetSyntaxAsync()`
4. Extract `node.ToString()` for the source text (prepend XML doc trivia if present)
5. Get file path and line span from `node.GetLocation().GetLineSpan()`
6. Return list of `SymbolSourceInfo` objects

### New Model

```csharp
public class SymbolSourceInfo
{
    public string? SymbolName { get; set; }
    public string? Kind { get; set; }
    public string? FilePath { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string? Source { get; set; }
}
```

### Query Type

`queryType: "getsymbolsource"`

### MCP Output Format

```
=== MyClass.MyMethod ===
File: src/Services/MyClass.cs (lines 42-58)

public async Task<string> MyMethod(int id)
{
    var result = await _repo.GetAsync(id);
    return result.Name;
}
```

## Feature 2: Enum Values in `get_type_members`

### Purpose

When querying enum type members, the current output shows field names but not their integer values. This forces a `Read` call to see the actual values.

### Changes

**Model change** — add `Value` field to `MemberInfo`:

```csharp
public class MemberInfo
{
    // ... existing fields ...
    public string? Value { get; set; }  // For enum members and const fields
}
```

**Service change** — in `SymbolQueryService.GetTypeMembersAsync()`, after creating each `MemberInfo`:

```csharp
if (member is IFieldSymbol fieldSymbol && fieldSymbol.HasConstantValue)
{
    memberInfo.Value = fieldSymbol.ConstantValue?.ToString();
}
```

This covers both enum members and `const` fields.

### MCP Output Change

The compact formatter in `SymbolTools` will append `= value` for members that have a Value:

```
Fields:
  Active = 0
  Inactive = 1
  Deleted = 2
```

## Feature 3: `find_usages`

### Purpose

`find_references` requires a file position and returns full location details for every reference. For refactoring discovery ("what files use this type?"), a name-based lookup returning just file paths grouped by project is faster and more practical.

### MCP Tool Interface

```
Tool: find_usages
Inputs:
  - symbolName (string, required): Name of the symbol to find usages of
  - solutionName (string, optional): Target solution
Returns:
  - Total reference count
  - File paths grouped by project name
```

### VS Extension Implementation

New method in `SymbolQueryService`:

```csharp
public async Task<QueryResponse> FindUsagesAsync(QueryRequest request)
```

1. Find symbol(s) by name using `GetSymbolsWithName()` with same exact/ends-with strategy as `get_symbol_source`
2. If multiple symbols match, aggregate usages across all of them (useful for overloads)
3. Call `SymbolFinder.FindReferencesAsync(symbol, solution)` for each symbol
4. Collect unique file paths from reference locations only (exclude definition locations)
5. Group by project name (from the document's Project property)
6. Return `UsageInfo` with count and grouped paths

### New Model

```csharp
public class UsageInfo
{
    public string? SymbolName { get; set; }
    public int TotalReferences { get; set; }
    public int FileCount { get; set; }
    public Dictionary<string, List<string>>? FilesByProject { get; set; }  // projectName -> [filePaths]
}
```

### Query Type

`queryType: "findusages"`

### MCP Output Format

```
Symbol: EntityType (12 references in 8 files)

MyProject.Core (3 files):
  src/Models/Entity.cs
  src/Services/EntityService.cs
  src/Repositories/EntityRepository.cs

MyProject.Tests (5 files):
  tests/EntityServiceTests.cs
  tests/EntityRepositoryTests.cs
  ...
```

## Layers Modified

| Layer | File | Changes |
|-------|------|---------|
| Models | `RoslynBridge/Models/SymbolModels.cs` | Add `SymbolSourceInfo`, `UsageInfo`, add `Value` to `MemberInfo` |
| Constants | `RoslynBridge/Constants/ServerConstants.cs` | Add `GetSymbolSource` and `FindUsages` query type constants |
| VS Extension | `RoslynBridge/Services/SymbolQueryService.cs` | Add `GetSymbolSourceAsync()`, `FindUsagesAsync()`, enum value extraction |
| VS Extension | `RoslynBridge/Services/RoslynQueryService.cs` | Route new query types to SymbolQueryService |
| WebAPI | Passthrough only (uses generic `/api/roslyn/query`) | No changes needed |
| MCP Client | `RoslynBridge.Mcp/Services/IRoslynWebApiClient.cs` | Add `GetSymbolSourceAsync()`, `FindUsagesAsync()` |
| MCP Client | `RoslynBridge.Mcp/Services/RoslynWebApiClient.cs` | Implement new client methods |
| MCP Tools | `RoslynBridge.Mcp/Tools/SymbolTools.cs` | Add `get_symbol_source`, `find_usages` tools, update `get_type_members` formatter |

## Output Guardrails

`get_symbol_source` uses the same `TruncateOutput` pattern from `ProjectTools.cs` (max 50,000 chars). If a type declaration exceeds the limit, the output is truncated with a message indicating the symbol was too large and suggesting a more specific member name.

`find_usages` output is naturally compact (just file paths) and unlikely to hit limits, but the same truncation is applied as a safety net.

## CLAUDE.md Update

After implementation, update CLAUDE.md tool count from 30 to 32 and add the two new tools to the Symbols tool list.
