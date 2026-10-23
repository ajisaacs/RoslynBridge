using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for workspace operations
/// </summary>
[McpServerToolType]
public class WorkspaceTools
{
    private readonly IRoslynWebApiClient _client;

    public WorkspaceTools(IRoslynWebApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Refresh the workspace by reloading documents from disk
    /// </summary>
    [McpServerTool(Name = "refresh_workspace", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Refresh the Visual Studio workspace by reloading all open documents from disk. Use this after externally modifying files to ensure VS has the latest content for diagnostics and code analysis.")]
    public async Task<string> RefreshWorkspace(
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        [Description("Optional file path to refresh only a specific file")] string? filePath = null,
        CancellationToken ct = default)
    {
        var result = await _client.RefreshWorkspaceAsync(solutionName, filePath, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Format a document using Visual Studio's formatter
    /// </summary>
    [McpServerTool(Name = "format_document", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Format a source file using Visual Studio's code formatter. Applies the solution's formatting rules.")]
    public async Task<string> FormatDocument(
        [Description("Full path to the file to format")] string filePath,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.FormatDocumentAsync(filePath, solutionName, ct);
        return FormatResult(result);
    }

    private static string FormatResult(JsonDocument doc)
    {
        return ResultLimiter.ToJson(doc);
    }
}
