using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for diagnostics operations (errors, warnings, etc.)
/// </summary>
[McpServerToolType]
public class DiagnosticsTools
{
    private readonly IRoslynWebApiClient _client;

    public DiagnosticsTools(IRoslynWebApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Get compiler diagnostics (errors and warnings) from the solution
    /// </summary>
    [McpServerTool(Name = "get_diagnostics")]
    [Description("Get compiler diagnostics (errors and warnings) from the solution. Optionally filter by file path, severity, or apply pagination.")]
    public async Task<string> GetDiagnostics(
        [Description("Optional file path to filter diagnostics to a specific file")] string? filePath = null,
        [Description("Optional severity filter: error, warning, info, hidden (comma-separated for multiple)")] string? severity = null,
        [Description("Optional limit for pagination (number of results to return)")] int? limit = null,
        [Description("Optional offset for pagination (number of results to skip)")] int? offset = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetDiagnosticsAsync(filePath, severity, limit, offset, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get a summary of diagnostics counts by severity
    /// </summary>
    [McpServerTool(Name = "get_diagnostics_summary")]
    [Description("Get a summary of diagnostics with counts grouped by severity level (errors, warnings, info, hidden).")]
    public async Task<string> GetDiagnosticsSummary(
        [Description("Optional file path to filter diagnostics to a specific file")] string? filePath = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetDiagnosticsSummaryAsync(filePath, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get the count of diagnostics
    /// </summary>
    [McpServerTool(Name = "get_diagnostics_count")]
    [Description("Get the count of diagnostics, optionally filtered by file path and severity.")]
    public async Task<string> GetDiagnosticsCount(
        [Description("Optional file path to filter diagnostics to a specific file")] string? filePath = null,
        [Description("Optional severity filter: error, warning, info, hidden")] string? severity = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetDiagnosticsCountAsync(filePath, severity, ct);
        return FormatResult(result);
    }

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
