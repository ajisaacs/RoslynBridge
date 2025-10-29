using System.Text;
using System.Text.Json;
using RoslynBridge.WebApi.Models;

namespace RoslynBridge.WebApi.Services;

/// <summary>
/// HTTP client for communicating with the Roslyn Bridge Visual Studio plugin
/// </summary>
public class RoslynBridgeClient : IRoslynBridgeClient
{
    private readonly HttpClient _httpClient;
    private readonly IInstanceRegistryService _registryService;
    private readonly ILogger<RoslynBridgeClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RoslynBridgeClient(
        HttpClient httpClient,
        IInstanceRegistryService registryService,
        ILogger<RoslynBridgeClient> logger)
    {
        _httpClient = httpClient;
        _registryService = registryService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<RoslynQueryResponse> ExecuteQueryAsync(
        RoslynQueryRequest request,
        int? instancePort = null,
        string? solutionName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetPort = await ResolveInstancePortAsync(instancePort, request, solutionName);

            if (targetPort == null)
            {
                return new RoslynQueryResponse
                {
                    Success = false,
                    Error = "No Visual Studio instance available"
                };
            }

            _logger.LogInformation("Executing query: {QueryType} on port {Port}", request.QueryType, targetPort);

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"http://localhost:{targetPort}/query";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Query failed with status {StatusCode}: {Error}", response.StatusCode, errorContent);

                return new RoslynQueryResponse
                {
                    Success = false,
                    Error = $"Request failed with status {response.StatusCode}: {errorContent}"
                };
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<RoslynQueryResponse>(responseContent, _jsonOptions);

            if (result == null)
            {
                return new RoslynQueryResponse
                {
                    Success = false,
                    Error = "Failed to deserialize response"
                };
            }

            _logger.LogInformation("Query executed successfully: {Success}", result.Success);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while executing query");
            return new RoslynQueryResponse
            {
                Success = false,
                Error = $"Failed to connect to Roslyn Bridge server: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while executing query");
            return new RoslynQueryResponse
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}"
            };
        }
    }

    public async Task<bool> IsHealthyAsync(int? instancePort = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var targetPort = await ResolveInstancePortAsync(instancePort, null);

            if (targetPort == null)
            {
                return false;
            }

            var request = new RoslynQueryRequest { QueryType = "health" };
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"http://localhost:{targetPort}/health";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed");
            return false;
        }
    }

    /// <summary>
    /// Resolves which VS instance port to use based on provided hints
    /// </summary>
    private Task<int?> ResolveInstancePortAsync(int? explicitPort, RoslynQueryRequest? request, string? solutionName = null)
    {
        // If explicit port specified, use it
        if (explicitPort.HasValue)
        {
            return Task.FromResult<int?>(explicitPort.Value);
        }

        // Try to find instance by solution name
        if (!string.IsNullOrEmpty(solutionName))
        {
            var matchedInstances = _registryService.GetAllInstances()
                .Where(i => i.SolutionName != null &&
                           i.SolutionName.Equals(solutionName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchedInstances.Any())
            {
                _logger.LogDebug("Found instance by solution name: {SolutionName}, Port: {Port}", solutionName, matchedInstances[0].Port);
                return Task.FromResult<int?>(matchedInstances[0].Port);
            }

            _logger.LogWarning("No instance found for solution: {SolutionName}", solutionName);
        }

        // Try to find instance by solution path from request
        if (request != null && !string.IsNullOrEmpty(request.FilePath))
        {
            // Extract solution path by looking for .sln file in the path hierarchy
            var directory = Path.GetDirectoryName(request.FilePath);
            while (!string.IsNullOrEmpty(directory))
            {
                var solutionFiles = Directory.GetFiles(directory, "*.sln");
                if (solutionFiles.Length > 0)
                {
                    var instance = _registryService.GetBySolutionPath(solutionFiles[0]);
                    if (instance != null)
                    {
                        _logger.LogDebug("Found instance by solution path: {SolutionPath}", solutionFiles[0]);
                        return Task.FromResult<int?>(instance.Port);
                    }
                }
                directory = Path.GetDirectoryName(directory);
            }
        }

        // Fall back to first available instance
        var allInstances = _registryService.GetAllInstances().ToList();
        if (allInstances.Any())
        {
            _logger.LogDebug("Using first available instance: port {Port}", allInstances[0].Port);
            return Task.FromResult<int?>(allInstances[0].Port);
        }

        _logger.LogWarning("No Visual Studio instances registered");
        return Task.FromResult<int?>(null);
    }
}
