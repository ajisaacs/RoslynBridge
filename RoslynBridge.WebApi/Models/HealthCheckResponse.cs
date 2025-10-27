namespace RoslynBridge.WebApi.Models;

/// <summary>
/// Health check response for service status
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Overall health status
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Web API service status
    /// </summary>
    public string WebApiStatus { get; set; } = "Running";

    /// <summary>
    /// Visual Studio plugin connection status
    /// </summary>
    public string VsPluginStatus { get; set; } = "Unknown";

    /// <summary>
    /// Timestamp of the health check
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// API version
    /// </summary>
    public string Version { get; set; } = "1.0.0";
}
