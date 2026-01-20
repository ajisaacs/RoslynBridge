namespace RoslynBridge.Mcp.Configuration;

/// <summary>
/// Configuration options for the RoslynBridge MCP server
/// </summary>
public class RoslynBridgeOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "McpServer";

    /// <summary>
    /// Base URL for the RoslynBridge WebAPI (e.g., http://localhost:5000)
    /// </summary>
    public string WebApiBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
