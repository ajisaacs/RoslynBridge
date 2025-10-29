using RoslynBridge.WebApi.Models;

namespace RoslynBridge.WebApi.Services;

/// <summary>
/// Interface for communicating with the Roslyn Bridge Visual Studio plugin
/// </summary>
public interface IRoslynBridgeClient
{
    /// <summary>
    /// Execute a query against the Roslyn Bridge server
    /// </summary>
    /// <param name="request">The query request</param>
    /// <param name="instancePort">Optional port of specific VS instance to target</param>
    /// <param name="solutionName">Optional solution name to target specific VS instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The query response</returns>
    Task<RoslynQueryResponse> ExecuteQueryAsync(RoslynQueryRequest request, int? instancePort = null, string? solutionName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the Roslyn Bridge server is healthy
    /// </summary>
    /// <param name="instancePort">Optional port of specific VS instance to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if healthy, false otherwise</returns>
    Task<bool> IsHealthyAsync(int? instancePort = null, CancellationToken cancellationToken = default);
}
