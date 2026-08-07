# Symbol Source, Enum Values & Find Usages Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three new capabilities to RoslynBridge: `get_symbol_source` (retrieve method/type source code by name), enum value display in `get_type_members`, and `find_usages` (lightweight file-path-only reference finder by name).

**Architecture:** All three features follow the existing RoslynBridge data flow: MCP Tool → WebAPI Client → WebAPI (passthrough) → VS Extension Service. The VS extension uses Roslyn's `GetSymbolsWithName()` + `DeclaringSyntaxReferences` for source retrieval and `SymbolFinder.FindReferencesAsync()` for usages. The WebAPI layer requires no changes since everything routes through the generic `/api/roslyn/query` POST endpoint.

**Tech Stack:** .NET Framework 4.8 (VS Extension), .NET 8.0 (WebAPI + MCP), Roslyn `Microsoft.CodeAnalysis`, MCP SDK `ModelContextProtocol.Server`

**Spec:** `docs/superpowers/specs/2026-03-17-symbol-source-enum-values-find-usages-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `RoslynBridge/Models/SymbolModels.cs` | Modify | Add `SymbolSourceInfo`, `UsageInfo` models; add `Value` to `MemberInfo` |
| `RoslynBridge/Constants/ServerConstants.cs` | Modify | Add `GetSymbolSource` and `FindUsages` query type constants |
| `RoslynBridge/Services/SymbolQueryService.cs` | Modify | Add `GetSymbolSourceAsync()`, `FindUsagesAsync()`, enum value extraction in `GetTypeMembersAsync()` |
| `RoslynBridge/Services/RoslynQueryService.cs` | Modify | Route two new query types |
| `RoslynBridge.Mcp/Services/IRoslynWebApiClient.cs` | Modify | Add two new interface methods |
| `RoslynBridge.Mcp/Services/RoslynWebApiClient.cs` | Modify | Implement two new HTTP client methods |
| `RoslynBridge.Mcp/Tools/SymbolTools.cs` | Modify | Add `get_symbol_source` and `find_usages` MCP tools; update `FormatTypeMembersCompact` for enum values |
| `CLAUDE.md` | Modify | Update tool count and list |

---

### Task 1: Models & Constants (VS Extension layer)

**Files:**
- Modify: `RoslynBridge/Models/SymbolModels.cs`
- Modify: `RoslynBridge/Constants/ServerConstants.cs`

- [ ] **Step 1: Add `Value` property to `MemberInfo`**

In `RoslynBridge/Models/SymbolModels.cs`, add after `IsOverride` (line 30):

```csharp
public string? Value { get; set; }
```

- [ ] **Step 2: Add `SymbolSourceInfo` model**

In `RoslynBridge/Models/SymbolModels.cs`, add after the `SymbolContextInfo` class (after line 58):

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

public class UsageInfo
{
    public string? SymbolName { get; set; }
    public int TotalReferences { get; set; }
    public int FileCount { get; set; }
    public Dictionary<string, List<string>>? FilesByProject { get; set; }
}
```

- [ ] **Step 3: Add query type constants**

In `RoslynBridge/Constants/ServerConstants.cs`, add inside the `QueryTypes` class after `SearchCode` (line 35):

```csharp
public const string GetSymbolSource = "getsymbolsource";
public const string FindUsages = "findusages";
```

- [ ] **Step 4: Build the VS Extension project to verify compilation**

Run: `dotnet build RoslynBridge/RoslynBridge.csproj` (or check it compiles via VS)

- [ ] **Step 5: Commit**

```bash
git add RoslynBridge/Models/SymbolModels.cs RoslynBridge/Constants/ServerConstants.cs
git commit -m "feat(models): add SymbolSourceInfo, UsageInfo, Value on MemberInfo, and new query type constants"
```

---

### Task 2: Enum values in `GetTypeMembersAsync` (VS Extension)

**Files:**
- Modify: `RoslynBridge/Services/SymbolQueryService.cs`

- [ ] **Step 1: Add enum/const value extraction**

In `SymbolQueryService.cs`, inside `GetTypeMembersAsync()`, modify the `memberInfos` LINQ projection (lines 160-175). After the existing property assignments and before the closing `}).ToList()`, add the `Value` assignment:

Replace the `memberInfos` projection (lines 160-175):

```csharp
var memberInfos = members.Select(m =>
{
    var info = new MemberInfo
    {
        Name = m.Name,
        Kind = m.Kind.ToString(),
        ReturnType = (m as IMethodSymbol)?.ReturnType.ToDisplayString() ??
                    (m as IPropertySymbol)?.Type.ToDisplayString() ??
                    (m as IFieldSymbol)?.Type.ToDisplayString(),
        Signature = m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        Documentation = m.GetDocumentationCommentXml(),
        Modifiers = GetModifiers(m),
        Accessibility = m.DeclaredAccessibility.ToString(),
        IsStatic = m.IsStatic,
        IsAbstract = m.IsAbstract,
        IsVirtual = m.IsVirtual,
        IsOverride = m.IsOverride
    };
    if (m is IFieldSymbol fieldSymbol && fieldSymbol.HasConstantValue)
    {
        info.Value = fieldSymbol.ConstantValue?.ToString();
    }
    return info;
}).ToList();
```

- [ ] **Step 2: Build to verify compilation**

- [ ] **Step 3: Commit**

```bash
git add RoslynBridge/Services/SymbolQueryService.cs
git commit -m "feat(symbols): include enum/const values in GetTypeMembers responses"
```

---

### Task 3: `GetSymbolSourceAsync` (VS Extension)

**Files:**
- Modify: `RoslynBridge/Services/SymbolQueryService.cs`
- Modify: `RoslynBridge/Services/RoslynQueryService.cs`

- [ ] **Step 1: Add `GetSymbolSourceAsync` method to `SymbolQueryService`**

Add at the end of `SymbolQueryService` class, before the closing `}`:

```csharp
public async Task<QueryResponse> GetSymbolSourceAsync(QueryRequest request)
{
    if (string.IsNullOrEmpty(request.SymbolName))
    {
        return CreateErrorResponse("SymbolName is required");
    }

    var results = new List<SymbolSourceInfo>();
    var searchName = request.SymbolName!;

    foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null) continue;

        // Try exact match first, then ends-with fallback
        var symbols = compilation.GetSymbolsWithName(
            name => name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.All);

        if (!symbols.Any())
        {
            symbols = compilation.GetSymbolsWithName(
                name => searchName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase) ||
                        name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.All);
        }

        foreach (var symbol in symbols)
        {
            // Check if fully qualified name matches for dotted names
            if (searchName.Contains("."))
            {
                var fullName = symbol.ToDisplayString();
                if (!fullName.EndsWith(searchName, StringComparison.OrdinalIgnoreCase) &&
                    !fullName.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
            {
                var node = await syntaxRef.GetSyntaxAsync();
                var lineSpan = node.GetLocation().GetLineSpan();

                // Get source: node.ToString() excludes leading/trailing trivia from adjacent declarations
                var source = node.ToString();

                // Prepend XML doc comment trivia if present
                var leadingTrivia = node.GetLeadingTrivia();
                var docComment = leadingTrivia
                    .Where(t => t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineDocumentationCommentTrivia))
                    .Select(t => t.ToFullString())
                    .FirstOrDefault();

                if (docComment != null)
                {
                    source = docComment + source;
                }

                results.Add(new SymbolSourceInfo
                {
                    SymbolName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    Kind = symbol.Kind.ToString(),
                    FilePath = lineSpan.Path,
                    StartLine = lineSpan.StartLinePosition.Line + 1,
                    EndLine = lineSpan.EndLinePosition.Line + 1,
                    Source = source
                });
            }
        }
    }

    if (!results.Any())
    {
        return CreateErrorResponse($"Symbol '{request.SymbolName}' not found");
    }

    // Deduplicate by file+line (same symbol found via multiple projects)
    results = results
        .GroupBy(r => $"{r.FilePath}:{r.StartLine}")
        .Select(g => g.First())
        .ToList();

    return CreateSuccessResponse(results);
}
```

- [ ] **Step 2: Add `GetSymbolSource` routing in `RoslynQueryService`**

In `RoslynQueryService.cs`, add after the `GetSymbolContext` case (line 91):

```csharp
QueryTypes.GetSymbolSource => await _symbolService.GetSymbolSourceAsync(request),
```

- [ ] **Step 3: Build to verify compilation**

- [ ] **Step 4: Commit**

```bash
git add RoslynBridge/Services/SymbolQueryService.cs RoslynBridge/Services/RoslynQueryService.cs
git commit -m "feat(symbols): add GetSymbolSource and route new query types"
```

---

### Task 4: `FindUsagesAsync` (VS Extension)

**Files:**
- Modify: `RoslynBridge/Services/SymbolQueryService.cs`

- [ ] **Step 1: Add `FindUsages` routing in `RoslynQueryService`**

In `RoslynQueryService.cs`, add after the `GetSymbolSource` case just added:

```csharp
QueryTypes.FindUsages => await _symbolService.FindUsagesAsync(request),
```

- [ ] **Step 2: Add `FindUsagesAsync` method to `SymbolQueryService`**

Add at the end of `SymbolQueryService` class, before the closing `}`:

```csharp
public async Task<QueryResponse> FindUsagesAsync(QueryRequest request)
{
    if (string.IsNullOrEmpty(request.SymbolName))
    {
        return CreateErrorResponse("SymbolName is required");
    }

    if (Workspace?.CurrentSolution == null)
    {
        return CreateErrorResponse("Workspace not available");
    }

    var searchName = request.SymbolName!;
    var allSymbols = new List<ISymbol>();

    foreach (var project in Workspace.CurrentSolution.Projects)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null) continue;

        // Exact match first, then ends-with fallback
        var symbols = compilation.GetSymbolsWithName(
            name => name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.All);

        if (!symbols.Any())
        {
            symbols = compilation.GetSymbolsWithName(
                name => searchName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase) ||
                        name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
                SymbolFilter.All);
        }

        foreach (var symbol in symbols)
        {
            if (searchName.Contains("."))
            {
                var fullName = symbol.ToDisplayString();
                if (!fullName.EndsWith(searchName, StringComparison.OrdinalIgnoreCase) &&
                    !fullName.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            allSymbols.Add(symbol);
        }
    }

    if (!allSymbols.Any())
    {
        return CreateErrorResponse($"Symbol '{request.SymbolName}' not found");
    }

    // Deduplicate symbols (same symbol found via multiple project compilations)
    var uniqueSymbols = allSymbols
        .GroupBy(s => s.ToDisplayString())
        .Select(g => g.First())
        .ToList();

    var filesByProject = new Dictionary<string, HashSet<string>>();
    var totalReferences = 0;

    foreach (var symbol in uniqueSymbols)
    {
        var references = await SymbolFinder.FindReferencesAsync(symbol, Workspace.CurrentSolution);
        foreach (var refSymbol in references)
        {
            foreach (var location in refSymbol.Locations)
            {
                totalReferences++;
                var doc = location.Document;
                var projectName = doc.Project.Name;
                var filePath = doc.FilePath ?? doc.Name;

                if (!filesByProject.ContainsKey(projectName))
                    filesByProject[projectName] = new HashSet<string>();
                filesByProject[projectName].Add(filePath);
            }
        }
    }

    var result = new UsageInfo
    {
        SymbolName = uniqueSymbols.First().ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
        TotalReferences = totalReferences,
        FileCount = filesByProject.Values.Sum(s => s.Count),
        FilesByProject = filesByProject.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.OrderBy(f => f).ToList())
    };

    return CreateSuccessResponse(result);
}
```

- [ ] **Step 3: Build to verify everything compiles**

- [ ] **Step 4: Commit**

```bash
git add RoslynBridge/Services/SymbolQueryService.cs RoslynBridge/Services/RoslynQueryService.cs
git commit -m "feat(symbols): add FindUsages for lightweight file-path-only reference search"
```

---

### Task 5: MCP Client Layer

**Files:**
- Modify: `RoslynBridge.Mcp/Services/IRoslynWebApiClient.cs`
- Modify: `RoslynBridge.Mcp/Services/RoslynWebApiClient.cs`

- [ ] **Step 1: Add interface methods to `IRoslynWebApiClient`**

In `IRoslynWebApiClient.cs`, add after the `GetNamespaceTypesAsync` method (line 48), still inside the "Advanced Symbol Operations" section:

```csharp
Task<JsonDocument> GetSymbolSourceAsync(string symbolName, string? solutionName = null, CancellationToken ct = default);
Task<JsonDocument> FindUsagesAsync(string symbolName, string? solutionName = null, CancellationToken ct = default);
```

- [ ] **Step 2: Implement methods in `RoslynWebApiClient`**

In `RoslynWebApiClient.cs`, add after the `GetNamespaceTypesAsync` method (after line 282), still inside the "Advanced Symbol Operations" section:

```csharp
public async Task<JsonDocument> GetSymbolSourceAsync(string symbolName, string? solutionName = null, CancellationToken ct = default)
{
    var query = BuildQuery(("solutionName", solutionName));
    var body = new Dictionary<string, object>
    {
        ["queryType"] = "getsymbolsource",
        ["symbolName"] = symbolName
    };
    return await PostAsync($"/api/roslyn/query{query}", body, ct);
}

public async Task<JsonDocument> FindUsagesAsync(string symbolName, string? solutionName = null, CancellationToken ct = default)
{
    var query = BuildQuery(("solutionName", solutionName));
    var body = new Dictionary<string, object>
    {
        ["queryType"] = "findusages",
        ["symbolName"] = symbolName
    };
    return await PostAsync($"/api/roslyn/query{query}", body, ct);
}
```

- [ ] **Step 3: Build MCP project to verify**

Run: `dotnet build RoslynBridge.Mcp`

- [ ] **Step 4: Commit**

```bash
git add RoslynBridge.Mcp/Services/IRoslynWebApiClient.cs RoslynBridge.Mcp/Services/RoslynWebApiClient.cs
git commit -m "feat(mcp-client): add GetSymbolSource and FindUsages HTTP client methods"
```

---

### Task 6: MCP Tools (get_symbol_source, find_usages, update formatter)

**Files:**
- Modify: `RoslynBridge.Mcp/Tools/SymbolTools.cs`

- [ ] **Step 1: Update `FormatTypeMembersCompact` to show enum/const values**

In `SymbolTools.cs`, modify the member formatting loop inside `FormatTypeMembersCompact` (around line 124). Replace:

```csharp
var line = $"  {prefix} {sig}";
```

With:

```csharp
var value = member.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
var line = value != null ? $"  {prefix} {sig} = {value}" : $"  {prefix} {sig}";
```

- [ ] **Step 2: Add `get_symbol_source` MCP tool**

Add after the `GetNamespaceTypes` method (before the `FormatResult` helper), in `SymbolTools.cs`:

```csharp
/// <summary>
/// Get the source code of a symbol by name
/// </summary>
[McpServerTool(Name = "get_symbol_source")]
[Description("Get the actual source code (implementation) of a symbol by name. Returns the full declaration including method bodies. Supports types, methods, properties, fields, enums. For 'MyClass.MyMethod' syntax, returns just that member. Partial types return each partial declaration separately.")]
public async Task<string> GetSymbolSource(
    [Description("Symbol name - simple (e.g., 'MyClass') or dotted (e.g., 'MyClass.MyMethod')")] string symbolName,
    [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
    CancellationToken ct = default)
{
    var result = await _client.GetSymbolSourceAsync(symbolName, solutionName, ct);
    return FormatSymbolSource(result, symbolName);
}

private static string FormatSymbolSource(JsonDocument doc, string symbolName)
{
    var root = doc.RootElement;
    if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
        return FormatResult(doc);

    if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        return FormatResult(doc);

    var sb = new System.Text.StringBuilder();
    var count = data.GetArrayLength();
    if (count == 0)
        return $"No source found for '{symbolName}'";

    foreach (var item in data.EnumerateArray())
    {
        var name = item.TryGetProperty("symbolName", out var n) ? n.GetString() ?? "" : "";
        var kind = item.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        var filePath = item.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
        var startLine = item.TryGetProperty("startLine", out var sl) ? sl.GetInt32() : 0;
        var endLine = item.TryGetProperty("endLine", out var el) ? el.GetInt32() : 0;
        var source = item.TryGetProperty("source", out var src) ? src.GetString() ?? "" : "";

        sb.AppendLine($"=== {name} ({kind}) ===");
        sb.AppendLine($"File: {filePath} (lines {startLine}-{endLine})");
        sb.AppendLine();
        sb.AppendLine(source);

        if (count > 1)
            sb.AppendLine();
    }

    return TruncateOutput(sb);
}

private const int MaxOutputChars = 50_000;

private static string TruncateOutput(System.Text.StringBuilder sb)
{
    var result = sb.ToString().TrimEnd();
    if (result.Length <= MaxOutputChars)
        return result;
    return result[..MaxOutputChars] + "\n\n...[output truncated at 50K chars — try a more specific symbol name like 'MyClass.MyMethod']";
}
```

- [ ] **Step 3: Add `find_usages` MCP tool**

Add after the `GetSymbolSource` method:

```csharp
/// <summary>
/// Find all files that reference a symbol (lightweight alternative to find_references)
/// </summary>
[McpServerTool(Name = "find_usages")]
[Description("Find all files that reference a symbol, grouped by project. Lightweight alternative to find_references — takes a symbol name (not file position) and returns just file paths, not line-level locations. Ideal for refactoring discovery ('what files use this type?').")]
public async Task<string> FindUsages(
    [Description("Symbol name to find usages of (e.g., 'EntityType', 'MyClass.MyMethod')")] string symbolName,
    [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
    CancellationToken ct = default)
{
    var result = await _client.FindUsagesAsync(symbolName, solutionName, ct);
    return FormatUsages(result, symbolName);
}

private static string FormatUsages(JsonDocument doc, string symbolName)
{
    var root = doc.RootElement;
    if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
        return FormatResult(doc);

    if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        return FormatResult(doc);

    var name = data.TryGetProperty("symbolName", out var n) ? n.GetString() ?? symbolName : symbolName;
    var totalRefs = data.TryGetProperty("totalReferences", out var tr) ? tr.GetInt32() : 0;
    var fileCount = data.TryGetProperty("fileCount", out var fc) ? fc.GetInt32() : 0;

    var sb = new System.Text.StringBuilder();
    sb.AppendLine($"Symbol: {name} ({totalRefs} references in {fileCount} files)");
    sb.AppendLine();

    if (data.TryGetProperty("filesByProject", out var fbp) && fbp.ValueKind == JsonValueKind.Object)
    {
        foreach (var project in fbp.EnumerateObject())
        {
            var files = project.Value.EnumerateArray().Select(f => f.GetString() ?? "").ToList();
            sb.AppendLine($"{project.Name} ({files.Count} files):");
            foreach (var file in files)
                sb.AppendLine($"  {file}");
            sb.AppendLine();
        }
    }

    return TruncateOutput(sb);
}
```

- [ ] **Step 4: Build MCP project**

Run: `dotnet build RoslynBridge.Mcp`

- [ ] **Step 5: Commit**

```bash
git add RoslynBridge.Mcp/Tools/SymbolTools.cs
git commit -m "feat(mcp): add get_symbol_source and find_usages tools, show enum values in get_type_members"
```

---

### Task 7: Update CLAUDE.md and publish

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md tool count and list**

Change `30 total` to `32 total` in the "Available MCP Tools" heading.

Change `Symbols (11)` to `Symbols (13)`.

Add to the Symbols list:
```
- `get_symbol_source` - Get source code of a symbol by name
- `find_usages` - Find files referencing a symbol (by name, returns file paths grouped by project)
```

- [ ] **Step 2: Build the full solution to verify everything compiles**

Run: `dotnet build RoslynBridge.Mcp -c Release`

- [ ] **Step 3: Publish updated MCP server**

Run: `dotnet publish RoslynBridge.Mcp/RoslynBridge.Mcp.csproj -c Release -o "$USERPROFILE/.claude/mcp/RoslynBridge.Mcp"`

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with new get_symbol_source and find_usages tools"
```
