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
    [Description("Search for symbols by name across the solution. Supports partial matching.")]
    public async Task<string> SearchSymbol(
        [Description("Symbol name or pattern to search for")] string symbolName,
        [Description("Optional symbol kind filter (e.g., 'Method', 'Class', 'Property')")] string? kind = null,
        CancellationToken ct = default)
    {
        var result = await _client.SearchSymbolAsync(symbolName, kind, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get all members of a type (methods, properties, fields, etc.)
    /// </summary>
    [McpServerTool(Name = "get_type_members")]
    [Description("Get all members of a type including methods, properties, fields, and events. Optionally include inherited members.")]
    public async Task<string> GetTypeMembers(
        [Description("Fully qualified type name (e.g., 'MyNamespace.MyClass')")] string typeName,
        [Description("Include inherited members from base types")] bool includeInherited = false,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetTypeMembersAsync(typeName, includeInherited, solutionName, ct);
        return FormatResult(result);
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
    [Description("Search code using regex patterns. More powerful than simple symbol search for finding code structures and patterns.")]
    public async Task<string> SearchCode(
        [Description("Regex pattern to search for (e.g., 'async.*Task', 'DbContext')")] string pattern,
        [Description("Scope filter: 'all', 'methods', 'classes', 'properties'")] string? scope = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.SearchCodeAsync(pattern, scope, solutionName, ct);
        return FormatResult(result);
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

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
