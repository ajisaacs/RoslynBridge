using System.Collections.Concurrent;
using RoslynBridge.WebApi.Models;

namespace RoslynBridge.WebApi.Services;

/// <summary>
/// Thread-safe in-memory registry for Visual Studio instances
/// </summary>
public class InstanceRegistryService : IInstanceRegistryService
{
    private readonly ConcurrentDictionary<int, VSInstanceInfo> _instances = new();
    private readonly ILogger<InstanceRegistryService> _logger;

    public InstanceRegistryService(ILogger<InstanceRegistryService> logger)
    {
        _logger = logger;
    }

    public void Register(VSInstanceInfo instance)
    {
        instance.RegisteredAt = DateTime.UtcNow;
        instance.LastHeartbeat = DateTime.UtcNow;

        _instances.AddOrUpdate(instance.ProcessId, instance, (_, existing) =>
        {
            // Update existing instance
            existing.Port = instance.Port;
            existing.SolutionPath = instance.SolutionPath;
            existing.SolutionName = instance.SolutionName;
            existing.Projects = instance.Projects;
            existing.LastHeartbeat = DateTime.UtcNow;
            return existing;
        });

        _logger.LogInformation(
            "Registered VS instance: PID={ProcessId}, Port={Port}, Solution={Solution}",
            instance.ProcessId,
            instance.Port,
            instance.SolutionName ?? "None");
    }

    public bool Unregister(int processId)
    {
        var removed = _instances.TryRemove(processId, out var instance);

        if (removed)
        {
            _logger.LogInformation(
                "Unregistered VS instance: PID={ProcessId}, Solution={Solution}",
                processId,
                instance?.SolutionName ?? "None");
        }

        return removed;
    }

    public bool UpdateHeartbeat(int processId)
    {
        if (_instances.TryGetValue(processId, out var instance))
        {
            instance.LastHeartbeat = DateTime.UtcNow;
            _logger.LogDebug("Updated heartbeat for VS instance: PID={ProcessId}", processId);
            return true;
        }

        return false;
    }

    public IEnumerable<VSInstanceInfo> GetAllInstances()
    {
        return _instances.Values.ToList();
    }

    public VSInstanceInfo? GetByProcessId(int processId)
    {
        _instances.TryGetValue(processId, out var instance);
        return instance;
    }

    public VSInstanceInfo? GetBySolutionPath(string solutionPath)
    {
        if (string.IsNullOrEmpty(solutionPath))
            return null;

        var normalizedPath = Path.GetFullPath(solutionPath).ToLowerInvariant();

        return _instances.Values.FirstOrDefault(i =>
            !string.IsNullOrEmpty(i.SolutionPath) &&
            Path.GetFullPath(i.SolutionPath).ToLowerInvariant() == normalizedPath);
    }

    public VSInstanceInfo? GetByPort(int port)
    {
        return _instances.Values.FirstOrDefault(i => i.Port == port);
    }

    public void RemoveStaleInstances(TimeSpan timeout)
    {
        var cutoff = DateTime.UtcNow - timeout;
        var staleInstances = _instances.Values
            .Where(i => i.LastHeartbeat < cutoff)
            .ToList();

        foreach (var instance in staleInstances)
        {
            if (_instances.TryRemove(instance.ProcessId, out _))
            {
                _logger.LogWarning(
                    "Removed stale VS instance: PID={ProcessId}, LastHeartbeat={LastHeartbeat}",
                    instance.ProcessId,
                    instance.LastHeartbeat);
            }
        }
    }
}
