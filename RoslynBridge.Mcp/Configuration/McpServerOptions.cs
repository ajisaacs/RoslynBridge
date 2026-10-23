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
    /// Base URL for the RoslynBridge WebAPI (e.g., http://localhost:5001)
    /// </summary>
    public string WebApiBaseUrl { get; set; } = "http://localhost:5001";

    /// <summary>
    /// Request timeout in seconds. Kept above the WebAPI's own per-query limits
    /// (60s for queries, 11 min for builds/package operations) so those surface first.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 720;
}
