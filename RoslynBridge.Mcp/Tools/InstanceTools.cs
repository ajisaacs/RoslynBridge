using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for Visual Studio instance management
/// </summary>
[McpServerToolType]
public class InstanceTools
{
    private readonly IRoslynWebApiClient _client;

    public InstanceTools(IRoslynWebApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// List all registered Visual Studio instances
    /// </summary>
    [McpServerTool(Name = "list_instances")]
    [Description("List all registered Visual Studio instances with their solution information and connection status.")]
    public async Task<string> ListInstances(CancellationToken ct = default)
    {
        var result = await _client.ListInstancesAsync(ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get instance by solution path
    /// </summary>
    [McpServerTool(Name = "get_instance_by_solution")]
    [Description("Find the Visual Studio instance that has a specific solution open.")]
    public async Task<string> GetInstanceBySolution(
        [Description("Full path to the solution file (.sln)")] string solutionPath,
        CancellationToken ct = default)
    {
        var result = await _client.GetInstanceBySolutionAsync(solutionPath, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Check health of the RoslynBridge service
    /// </summary>
    [McpServerTool(Name = "check_health")]
    [Description("Check the health status of the RoslynBridge WebAPI service.")]
    public async Task<string> CheckHealth(CancellationToken ct = default)
    {
        var result = await _client.CheckHealthAsync(ct);
        return FormatResult(result);
    }

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
