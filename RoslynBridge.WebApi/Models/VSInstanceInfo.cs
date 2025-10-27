namespace RoslynBridge.WebApi.Models;

/// <summary>
/// Information about a registered Visual Studio instance
/// </summary>
public class VSInstanceInfo
{
    /// <summary>
    /// The port number where this VS instance is listening
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// The process ID of the Visual Studio instance
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// The solution file path (if any solution is open)
    /// </summary>
    public string? SolutionPath { get; set; }

    /// <summary>
    /// The solution name (if any solution is open)
    /// </summary>
    public string? SolutionName { get; set; }

    /// <summary>
    /// When this instance was registered
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// Last heartbeat time
    /// </summary>
    public DateTime LastHeartbeat { get; set; }

    /// <summary>
    /// List of project names in the solution
    /// </summary>
    public List<string> Projects { get; set; } = new();
}
