using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for symbol operations (go to definition, find references, etc.)
/// </summary>
[McpServerToolType]
public class SymbolTools
{
    private readonly IRoslynWebApiClient _client;

    public SymbolTools(IRoslynWebApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Get symbol information at a specific position in a file
    /// </summary>
    [McpServerTool(Name = "get_symbol")]
    [Description("Get detailed symbol information at a specific position in a file. Returns name, kind, type, documentation, and location.")]
    public async Task<string> GetSymbol(
        [Description("Full file path to the source file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (0-based)")] int column,
        [Description("Optional comma-separated list of fields to include (e.g., 'name,kind,location')")] string? fields = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetSymbolAsync(filePath, line, column, fields, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Find all references to a symbol
    /// </summary>
    [McpServerTool(Name = "find_references")]
    [Description("Find all references to the symbol at a specific position. Returns locations where the symbol is used.")]
    public async Task<string> FindReferences(
        [Description("Full file path to the source file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (0-based)")] int column,
        [Description("Optional comma-separated list of fields to include (e.g., 'filePath,line,column')")] string? fields = null,
        CancellationToken ct = default)
    {
        var result = await _client.FindReferencesAsync(filePath, line, column, fields, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get the count of references to a symbol
    /// </summary>
    [McpServerTool(Name = "get_references_count")]
    [Description("Get the count of references to the symbol at a specific position.")]
    public async Task<string> GetReferencesCount(
        [Description("Full file path to the source file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (0-based)")] int column,
        CancellationToken ct = default)
    {
        var result = await _client.GetReferencesCountAsync(filePath, line, column, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Search for symbols by name
    /// </summary>
    [McpServerTool(Name = "search_symbol")]
    [Description("Search for symbols by name across the solution (or a specific project). Supports partial matching. Excludes designer-generated files by default.")]
    public async Task<string> SearchSymbol(
        [Description("Symbol name or pattern to search for")] string symbolName,
        [Description("Optional symbol kind filter (e.g., 'Method', 'Class', 'Property')")] string? kind = null,
        [Description("Optional project name to restrict search scope (e.g., 'OpenNest.Engine')")] string? projectName = null,
        [Description("Exclude results from .Designer.cs and other generated files (default: true)")] bool excludeGenerated = true,
        CancellationToken ct = default)
    {
        var result = await _client.SearchSymbolAsync(symbolName, kind, projectName, excludeGenerated, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get all members of a type (methods, properties, fields, etc.)
    /// </summary>
    [McpServerTool(Name = "get_type_members")]
    [Description("Get all members of a type including methods, properties, fields, and events. Optionally filter by kind/accessibility. Returns compact one-line-per-member format by default.")]
    public async Task<string> GetTypeMembers(
        [Description("Fully qualified type name (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Include inherited members from base types")] bool includeInherited = false,
        [Description("Filter by member kind: Method, Property, Field, Event (comma-separated)")] string? kind = null,
        [Description("Filter by accessibility: Public, Private, Protected, Internal (comma-separated)")] string? accessibility = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetTypeMembersAsync(typeName, includeInherited, kind, accessibility, solutionName, ct);
        return FormatTypeMembersCompact(result, typeName);
    }

    private static string FormatTypeMembersCompact(JsonDocument doc, string typeName)
    {
        var root = doc.RootElement;
        if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            return FormatResult(doc);

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return FormatResult(doc);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Type: {typeName} ({data.GetArrayLength()} members)");
        sb.AppendLine(new string('-', 60));

        // Group by kind for readability
        var grouped = new Dictionary<string, List<string>>();
        foreach (var member in data.EnumerateArray())
        {
            var memberKind = member.TryGetProperty("kind", out var k) ? k.GetString() ?? "Unknown" : "Unknown";
            var sig = member.TryGetProperty("signature", out var s) ? s.GetString() ?? "" : "";
            var acc = member.TryGetProperty("accessibility", out var a) ? a.GetString() ?? "" : "";
            var isStatic = member.TryGetProperty("isStatic", out var st) && st.GetBoolean();

            var prefix = acc.ToLowerInvariant();
            if (isStatic) prefix += " static";

            var value = member.TryGetProperty("value", out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
            var line = value != null ? $"  {prefix} {sig} = {value}" : $"  {prefix} {sig}";

            if (!grouped.ContainsKey(memberKind))
                grouped[memberKind] = new List<string>();
            grouped[memberKind].Add(line);
        }

        // Output in a predictable order
        var kindOrder = new[] { "Method", "Property", "Field", "Event" };
        foreach (var kindName in kindOrder)
        {
            if (grouped.TryGetValue(kindName, out var items))
            {
                sb.AppendLine($"\n{kindName}s ({items.Count}):");
                foreach (var item in items)
                    sb.AppendLine(item);
                grouped.Remove(kindName);
            }
        }
        // Any remaining kinds
        foreach (var kvp in grouped)
        {
            sb.AppendLine($"\n{kvp.Key}s ({kvp.Value.Count}):");
            foreach (var item in kvp.Value)
                sb.AppendLine(item);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Get the type hierarchy (base types and derived types)
    /// </summary>
    [McpServerTool(Name = "get_type_hierarchy")]
    [Description("Get the inheritance hierarchy for a type, showing base classes/interfaces and derived types.")]
    public async Task<string> GetTypeHierarchy(
        [Description("Fully qualified type name (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Direction: 'up' (base types), 'down' (derived types), or 'both'")] string? direction = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetTypeHierarchyAsync(typeName, direction, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Find implementations of an interface or abstract class
    /// </summary>
    [McpServerTool(Name = "find_implementations")]
    [Description("Find all implementations of an interface or abstract class. Can search by name or by position in a file.")]
    public async Task<string> FindImplementations(
        [Description("Fully qualified symbol name (e.g., 'MyNamespace.IMyInterface')")] string? symbolName = null,
        [Description("Alternative: file path containing the symbol")] string? filePath = null,
        [Description("Alternative: line number (1-based) of the symbol")] int? line = null,
        [Description("Alternative: column number (0-based) of the symbol")] int? column = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.FindImplementationsAsync(symbolName, filePath, line, column, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get the call hierarchy for a method
    /// </summary>
    [McpServerTool(Name = "get_call_hierarchy")]
    [Description("Get the call hierarchy showing which methods call this method (callers) or which methods this method calls (callees).")]
    public async Task<string> GetCallHierarchy(
        [Description("Full file path to the source file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (0-based)")] int column,
        [Description("Direction: 'callers' (who calls this) or 'callees' (what this calls)")] string? direction = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetCallHierarchyAsync(filePath, line, column, direction, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Search code using regex patterns
    /// </summary>
    [McpServerTool(Name = "search_code")]
    [Description("Search source code using regex patterns. By default searches actual source text line-by-line (mode='text'), so patterns like 'override.*Shrink' or 'new List.*NestItem' work as expected. Use mode='symbols' to search symbol names only. Generated/designer files are excluded automatically.")]
    public async Task<string> SearchCode(
        [Description("Regex pattern to search for (e.g., 'override.*Shrink', 'new List.*NestItem', 'DbContext')")] string pattern,
        [Description("Optional project name to restrict search scope (e.g., 'OpenNest.Engine')")] string? projectName = null,
        [Description("Search mode: 'text' (default, searches source lines) or 'symbols' (searches symbol names only)")] string? mode = null,
        [Description("Scope filter for symbols mode: 'all', 'methods', 'classes', 'properties'")] string? scope = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.SearchCodeAsync(pattern, scope, projectName, mode, solutionName, ct);
        return FormatSearchCode(result, pattern);
    }

    private static string FormatSearchCode(JsonDocument doc, string pattern)
    {
        var root = doc.RootElement;
        if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            return FormatResult(doc);

        if (!root.TryGetProperty("data", out var data))
            return FormatResult(doc);

        // Text mode returns { matches: [...], truncated, message }
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("matches", out var matches))
        {
            var sb = new System.Text.StringBuilder();
            var truncated = data.TryGetProperty("truncated", out var t) && t.GetBoolean();
            var matchCount = matches.GetArrayLength();
            sb.AppendLine($"Pattern: {pattern} ({matchCount} match{(matchCount != 1 ? "es" : "")}{(truncated ? ", truncated" : "")})");
            sb.AppendLine();

            string? lastFile = null;
            foreach (var match in matches.EnumerateArray())
            {
                var filePath = match.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
                var project = match.TryGetProperty("projectName", out var pn) ? pn.GetString() ?? "" : "";
                var line = match.TryGetProperty("line", out var ln) ? ln.GetInt32() : 0;
                var text = match.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";

                var fileKey = $"{project}:{filePath}";
                if (fileKey != lastFile)
                {
                    sb.AppendLine($"--- {project}: {filePath} ---");
                    lastFile = fileKey;
                }
                sb.AppendLine($"  L{line}: {text}");
            }

            return TruncateOutput(sb);
        }

        // Symbols mode returns array of SymbolInfo
        return FormatResult(doc);
    }

    /// <summary>
    /// Get context information for a symbol at a position
    /// </summary>
    [McpServerTool(Name = "get_symbol_context")]
    [Description("Get rich context information for a symbol including its containing type, namespace, and surrounding code context.")]
    public async Task<string> GetSymbolContext(
        [Description("Full file path to the source file")] string filePath,
        [Description("Line number (1-based)")] int line,
        [Description("Column number (0-based)")] int column,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetSymbolContextAsync(filePath, line, column, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get all types in a namespace
    /// </summary>
    [McpServerTool(Name = "get_namespace_types")]
    [Description("Get all types (classes, interfaces, structs, enums) defined in a specific namespace.")]
    public async Task<string> GetNamespaceTypes(
        [Description("Namespace name (e.g., 'MyApp.Services')")] string namespaceName,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetNamespaceTypesAsync(namespaceName, solutionName, ct);
        return FormatResult(result);
    }

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

    /// <summary>
    /// Find all callers of a method or symbol by name
    /// </summary>
    [McpServerTool(Name = "find_callers")]
    [Description("Find all callers of a method by name. Returns caller method names, containing types, file locations, and line numbers. Use dotted names like 'MyClass.MyMethod' for precision, or just 'MyMethod' for broad search. This is the name-based equivalent of get_call_hierarchy — no file position needed.")]
    public async Task<string> FindCallers(
        [Description("Symbol name to find callers of (e.g., 'ShrinkFiller.Shrink', 'MyMethod')")] string symbolName,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.FindCallersAsync(symbolName, solutionName, ct);
        return FormatCallers(result, symbolName);
    }

    private static string FormatCallers(JsonDocument doc, string symbolName)
    {
        var root = doc.RootElement;
        if (!root.TryGetProperty("success", out var success) || !success.GetBoolean())
            return FormatResult(doc);

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return FormatResult(doc);

        var name = data.TryGetProperty("symbolName", out var n) ? n.GetString() ?? symbolName : symbolName;
        var totalCallers = data.TryGetProperty("totalCallers", out var tc) ? tc.GetInt32() : 0;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Callers of {name} ({totalCallers} call site{(totalCallers != 1 ? "s" : "")}):");
        sb.AppendLine();

        if (data.TryGetProperty("callers", out var callers) && callers.ValueKind == JsonValueKind.Array)
        {
            foreach (var caller in callers.EnumerateArray())
            {
                var sig = caller.TryGetProperty("callerSignature", out var cs) ? cs.GetString() ?? "" : "";
                var filePath = caller.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
                var line = caller.TryGetProperty("line", out var ln) ? ln.GetInt32() : 0;
                sb.AppendLine($"  {sig}");
                sb.AppendLine($"    {filePath}:{line}");
            }
        }

        return TruncateOutput(sb);
    }

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

    private const int MaxOutputChars = 50_000;

    private static string TruncateOutput(System.Text.StringBuilder sb)
    {
        var result = sb.ToString().TrimEnd();
        if (result.Length <= MaxOutputChars)
            return result;
        return result[..MaxOutputChars] + "\n\n...[output truncated at 50K chars — try a more specific symbol name like 'MyClass.MyMethod']";
    }

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
