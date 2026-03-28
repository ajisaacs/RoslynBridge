using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for code quality analysis (code smells, duplicates)
/// </summary>
[McpServerToolType]
public class CodeQualityTools
{
    private readonly IRoslynWebApiClient _client;

    public CodeQualityTools(IRoslynWebApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Get code smells (long methods, high complexity, etc.)
    /// </summary>
    [McpServerTool(Name = "get_code_smells")]
    [Description("Detect code smells like long methods, high cyclomatic complexity, too many parameters, deep nesting, and large classes.")]
    public async Task<string> GetCodeSmells(
        [Description("Optional file path to filter code smells to a specific file")] string? filePath = null,
        [Description("Optional project name to filter code smells")] string? projectName = null,
        [Description("Optional smell type filter: LongMethod, HighComplexity, TooManyParameters, DeepNesting, LargeClass, LongClass")] string? smellType = null,
        [Description("Optional severity filter: Low, Medium, High, Critical")] string? severity = null,
        [Description("Max results to return. 0 = unlimited. Default 50.")] int? top = 50,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetCodeSmellsAsync(filePath, projectName, smellType, severity, top, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get code smell summary with counts by type
    /// </summary>
    [McpServerTool(Name = "get_code_smell_summary")]
    [Description("Get a summary of code smells with counts grouped by smell type.")]
    public async Task<string> GetCodeSmellSummary(
        [Description("Optional file path to filter code smells to a specific file")] string? filePath = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetCodeSmellSummaryAsync(filePath, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Detect duplicate code blocks
    /// </summary>
    [McpServerTool(Name = "get_duplicates")]
    [Description("Detect duplicate or similar code blocks across the solution. Useful for identifying refactoring opportunities.")]
    public async Task<string> GetDuplicates(
        [Description("Minimum number of lines for a duplicate block (default: 5)")] int? minLines = null,
        [Description("Minimum similarity percentage 0-100 (default: 80)")] int? similarity = null,
        [Description("Optional class name filter (case-insensitive partial match)")] string? className = null,
        [Description("Optional namespace filter (case-insensitive partial match)")] string? namespaceName = null,
        [Description("Max results to return. 0 = unlimited. Default 50.")] int limit = 50,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetDuplicatesAsync(minLines, similarity, className, namespaceName, solutionName, ct);
        return ResultLimiter.LimitArrayResult(result, limit);
    }

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
