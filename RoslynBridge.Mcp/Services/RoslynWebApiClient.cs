using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoslynBridge.Mcp.Configuration;

namespace RoslynBridge.Mcp.Services;

/// <summary>
/// HTTP client wrapper for the RoslynBridge WebAPI
/// </summary>
public class RoslynWebApiClient : IRoslynWebApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RoslynWebApiClient> _logger;

    public RoslynWebApiClient(HttpClient httpClient, ILogger<RoslynWebApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Diagnostics
    public async Task<JsonDocument> GetDiagnosticsAsync(string? filePath = null, string? severity = null, int? limit = null, int? offset = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("filePath", filePath),
            ("severity", severity),
            ("limit", limit?.ToString()),
            ("offset", offset?.ToString()),
            ("solutionName", solutionName));
        return await GetAsync($"/api/roslyn/diagnostics{query}", ct);
    }

    public async Task<JsonDocument> GetDiagnosticsSummaryAsync(string? filePath = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("filePath", filePath), ("solutionName", solutionName));
        return await GetAsync($"/api/roslyn/diagnostics/summary{query}", ct);
    }

    public async Task<JsonDocument> GetDiagnosticsCountAsync(string? filePath = null, string? severity = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("filePath", filePath), ("severity", severity));
        return await GetAsync($"/api/roslyn/diagnostics/count{query}", ct);
    }

    // Symbols
    public async Task<JsonDocument> GetSymbolAsync(string filePath, int line, int column, string? fields = null, CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("filePath", filePath),
            ("line", line.ToString()),
            ("column", column.ToString()),
            ("fields", fields));
        return await GetAsync($"/api/roslyn/symbol{query}", ct);
    }

    public async Task<JsonDocument> FindReferencesAsync(string filePath, int line, int column, string? fields = null, CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("filePath", filePath),
            ("line", line.ToString()),
            ("column", column.ToString()),
            ("fields", fields));
        return await GetAsync($"/api/roslyn/references{query}", ct);
    }

    public async Task<JsonDocument> GetReferencesCountAsync(string filePath, int line, int column, CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("filePath", filePath),
            ("line", line.ToString()),
            ("column", column.ToString()));
        return await GetAsync($"/api/roslyn/references/count{query}", ct);
    }

    public async Task<JsonDocument> SearchSymbolAsync(string symbolName, string? kind = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("symbolName", symbolName), ("kind", kind));
        return await GetAsync($"/api/roslyn/symbol/search{query}", ct);
    }

    // Projects
    public async Task<JsonDocument> GetProjectsAsync(string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        return await GetAsync($"/api/roslyn/projects{query}", ct);
    }

    public async Task<JsonDocument> GetFilesAsync(string? projectName = null, string? path = null, string? pattern = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("projectName", projectName),
            ("path", path),
            ("pattern", pattern),
            ("solutionName", solutionName));
        return await GetAsync($"/api/roslyn/files{query}", ct);
    }

    public async Task<JsonDocument> GetSolutionOverviewAsync(CancellationToken ct = default)
    {
        return await GetAsync("/api/roslyn/solution/overview", ct);
    }

    public async Task<JsonDocument> BuildProjectAsync(string projectName, string? configuration = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("projectName", projectName), ("configuration", configuration));
        return await PostAsync($"/api/roslyn/project/build{query}", null, ct);
    }

    // Code Quality
    public async Task<JsonDocument> GetCodeSmellsAsync(string? filePath = null, string? projectName = null, string? smellType = null, string? severity = null, int? top = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("filePath", filePath),
            ("projectName", projectName),
            ("smellType", smellType),
            ("severity", severity),
            ("top", top?.ToString()),
            ("solutionName", solutionName));
        return await GetAsync($"/api/roslyn/codesmells{query}", ct);
    }

    public async Task<JsonDocument> GetCodeSmellSummaryAsync(string? filePath = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("filePath", filePath), ("solutionName", solutionName));
        return await GetAsync($"/api/roslyn/codesmells/summary{query}", ct);
    }

    public async Task<JsonDocument> GetDuplicatesAsync(int? minLines = null, int? similarity = null, string? className = null, string? namespaceName = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("minLines", minLines?.ToString()),
            ("similarity", similarity?.ToString()),
            ("className", className),
            ("namespace", namespaceName),
            ("solutionName", solutionName));
        return await GetAsync($"/api/roslyn/duplicates{query}", ct);
    }

    // Instances
    public async Task<JsonDocument> ListInstancesAsync(CancellationToken ct = default)
    {
        return await GetAsync("/api/instances", ct);
    }

    public async Task<JsonDocument> GetInstanceBySolutionAsync(string solutionPath, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionPath", solutionPath));
        return await GetAsync($"/api/instances/by-solution{query}", ct);
    }

    public async Task<JsonDocument> CheckHealthAsync(CancellationToken ct = default)
    {
        return await GetAsync("/api/health/ping", ct);
    }

    // Workspace
    public async Task<JsonDocument> RefreshWorkspaceAsync(string? solutionName = null, string? filePath = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName), ("filePath", filePath));
        return await PostAsync($"/api/roslyn/workspace/refresh{query}", null, ct);
    }

    public async Task<JsonDocument> FormatDocumentAsync(string filePath, CancellationToken ct = default)
    {
        return await PostAsync("/api/roslyn/format", filePath, ct);
    }

    // Advanced Symbol Operations (via query endpoint)
    public async Task<JsonDocument> GetTypeMembersAsync(string typeName, bool includeInherited = false, string? kind = null, string? accessibility = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "gettypemembers",
            ["symbolName"] = typeName
        };
        var parameters = new Dictionary<string, string>();
        if (includeInherited)
            parameters["includeInherited"] = "true";
        if (!string.IsNullOrEmpty(kind))
            parameters["kind"] = kind;
        if (!string.IsNullOrEmpty(accessibility))
            parameters["accessibility"] = accessibility;
        if (parameters.Count > 0)
            body["parameters"] = parameters;
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> GetTypeHierarchyAsync(string typeName, string? direction = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "gettypehierarchy",
            ["symbolName"] = typeName
        };
        if (!string.IsNullOrEmpty(direction))
        {
            body["parameters"] = new Dictionary<string, string> { ["direction"] = direction };
        }
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> FindImplementationsAsync(string? symbolName = null, string? filePath = null, int? line = null, int? column = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "findimplementations"
        };
        if (!string.IsNullOrEmpty(symbolName))
        {
            body["symbolName"] = symbolName;
        }
        else if (!string.IsNullOrEmpty(filePath) && line.HasValue && column.HasValue)
        {
            body["filePath"] = filePath;
            body["line"] = line.Value;
            body["column"] = column.Value;
        }
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> GetCallHierarchyAsync(string filePath, int line, int column, string? direction = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "getcallhierarchy",
            ["filePath"] = filePath,
            ["line"] = line,
            ["column"] = column
        };
        if (!string.IsNullOrEmpty(direction))
        {
            body["parameters"] = new Dictionary<string, string> { ["direction"] = direction };
        }
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> SearchCodeAsync(string pattern, string? scope = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "searchcode",
            ["symbolName"] = pattern
        };
        if (!string.IsNullOrEmpty(scope))
        {
            body["parameters"] = new Dictionary<string, string> { ["scope"] = scope };
        }
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> GetSymbolContextAsync(string filePath, int line, int column, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "getsymbolcontext",
            ["filePath"] = filePath,
            ["line"] = line,
            ["column"] = column
        };
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> GetNamespaceTypesAsync(string namespaceName, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "getnamespacetypes",
            ["symbolName"] = namespaceName
        };
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> GetSymbolSourceAsync(string symbolName, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "getsymbolsource",
            ["symbolName"] = symbolName
        };
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> FindUsagesAsync(string symbolName, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "findusages",
            ["symbolName"] = symbolName
        };
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    // Additional Project Operations (via query endpoint)
    public async Task<JsonDocument> CleanProjectAsync(string projectName, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "cleanproject",
            ["projectName"] = projectName
        };
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> RestorePackagesAsync(string projectName, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "restorepackages",
            ["projectName"] = projectName
        };
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> RemovePackageAsync(string projectName, string packageName, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("solutionName", solutionName));
        var body = new Dictionary<string, object>
        {
            ["queryType"] = "removenugetpackage",
            ["projectName"] = projectName,
            ["packageName"] = packageName
        };
        return await PostAsync($"/api/roslyn/query{query}", body, ct);
    }

    public async Task<JsonDocument> AddPackageAsync(string projectName, string packageName, string? version = null, string? solutionName = null, CancellationToken ct = default)
    {
        var query = BuildQuery(("projectName", projectName), ("packageName", packageName), ("version", version), ("solutionName", solutionName));
        return await PostAsync($"/api/roslyn/project/package/add{query}", null, ct);
    }

    // Helper methods
    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        _logger.LogDebug("GET {Path}", path);
        try
        {
            var response = await _httpClient.GetAsync(path, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Request failed: {StatusCode} - {Content}", response.StatusCode, content);
                return JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"HTTP {(int)response.StatusCode}: {content}"
                }));
            }

            return JsonDocument.Parse(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed: {Path}", path);
            return JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }));
        }
    }

    private async Task<JsonDocument> PostAsync(string path, object? body, CancellationToken ct)
    {
        _logger.LogDebug("POST {Path}", path);
        try
        {
            HttpContent? content = null;
            if (body != null)
            {
                var json = body is string s ? $"\"{s}\"" : JsonSerializer.Serialize(body);
                content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.PostAsync(path, content, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Request failed: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"HTTP {(int)response.StatusCode}: {responseContent}"
                }));
            }

            return JsonDocument.Parse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request failed: {Path}", path);
            return JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }));
        }
    }

    private static string BuildQuery(params (string key, string? value)[] parameters)
    {
        var sb = new StringBuilder();
        var first = true;

        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrEmpty(value)) continue;

            sb.Append(first ? '?' : '&');
            sb.Append(HttpUtility.UrlEncode(key));
            sb.Append('=');
            sb.Append(HttpUtility.UrlEncode(value));
            first = false;
        }

        return sb.ToString();
    }
}
