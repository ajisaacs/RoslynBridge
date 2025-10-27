using RoslynBridge.WebApi.Models;

namespace RoslynBridge.WebApi.Services;

/// <summary>
/// Service for managing registered Visual Studio instances
/// </summary>
public interface IInstanceRegistryService
{
    /// <summary>
    /// Register a new Visual Studio instance
    /// </summary>
    void Register(VSInstanceInfo instance);

    /// <summary>
    /// Unregister a Visual Studio instance by process ID
    /// </summary>
    bool Unregister(int processId);

    /// <summary>
    /// Update heartbeat for an instance
    /// </summary>
    bool UpdateHeartbeat(int processId);

    /// <summary>
    /// Get all registered instances
    /// </summary>
    IEnumerable<VSInstanceInfo> GetAllInstances();

    /// <summary>
    /// Get instance by process ID
    /// </summary>
    VSInstanceInfo? GetByProcessId(int processId);

    /// <summary>
    /// Get instance by solution path
    /// </summary>
    VSInstanceInfo? GetBySolutionPath(string solutionPath);

    /// <summary>
    /// Get instance by port
    /// </summary>
    VSInstanceInfo? GetByPort(int port);

    /// <summary>
    /// Remove stale instances (no heartbeat for specified timeout)
    /// </summary>
    void RemoveStaleInstances(TimeSpan timeout);
}
