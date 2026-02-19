using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for diagnostics operations (errors, warnings, etc.)
/// </summary>
[McpServerToolType]
public class DiagnosticsTools
{
    private readonly IRoslynWebApiClient _client;

    public DiagnosticsTools(IRoslynWebApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Get compiler diagnostics (errors and warnings) from the solution
    /// </summary>
    [McpServerTool(Name = "get_diagnostics")]
    [Description("Get compiler diagnostics (errors and warnings) from the solution. Optionally filter by file path, severity, or apply pagination.")]
    public async Task<string> GetDiagnostics(
        [Description("Optional file path to filter diagnostics to a specific file")] string? filePath = null,
        [Description("Optional severity filter: error, warning, info, hidden (comma-separated for multiple)")] string? severity = null,
        [Description("Optional limit for pagination (number of results to return)")] int? limit = null,
        [Description("Optional offset for pagination (number of results to skip)")] int? offset = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetDiagnosticsAsync(filePath, severity, limit, offset, solutionName, ct);
        return FormatDiagnosticsCompact(result);
    }

    /// <summary>
    /// Get a summary of diagnostics counts by severity
    /// </summary>
    [McpServerTool(Name = "get_diagnostics_summary")]
    [Description("Get a summary of diagnostics with counts grouped by severity level (errors, warnings, info, hidden).")]
    public async Task<string> GetDiagnosticsSummary(
        [Description("Optional file path to filter diagnostics to a specific file")] string? filePath = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetDiagnosticsSummaryAsync(filePath, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Get the count of diagnostics
    /// </summary>
    [McpServerTool(Name = "get_diagnostics_count")]
    [Description("Get the count of diagnostics, optionally filtered by file path and severity.")]
    public async Task<string> GetDiagnosticsCount(
        [Description("Optional file path to filter diagnostics to a specific file")] string? filePath = null,
        [Description("Optional severity filter: error, warning, info, hidden")] string? severity = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetDiagnosticsCountAsync(filePath, severity, ct);
        return FormatResult(result);
    }

    private static string FormatDiagnosticsCompact(JsonDocument doc)
    {
        var root = doc.RootElement;

        // Check for error response
        if (root.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
        {
            var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error";
            return $"Error: {error}";
        }

        // Get the diagnostics array from data property or root
        JsonElement diagnostics;
        if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
            diagnostics = dataProp;
        else if (root.ValueKind == JsonValueKind.Array)
            diagnostics = root;
        else
            return FormatResult(doc);

        var total = diagnostics.GetArrayLength();
        if (total == 0)
            return "No diagnostics found.";

        // Find common path prefix to shorten file paths
        var allPaths = new List<string>();
        foreach (var d in diagnostics.EnumerateArray())
        {
            if (d.TryGetProperty("location", out var loc) && loc.TryGetProperty("filePath", out var fp))
            {
                var path = fp.GetString();
                if (!string.IsNullOrEmpty(path))
                    allPaths.Add(path);
            }
        }
        var commonPrefix = FindCommonPathPrefix(allPaths);

        // Count by severity
        var severityCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Group by: id -> message -> list of locations
        var groups = new Dictionary<string, (string Message, string Severity, List<string> Locations)>();

        foreach (var d in diagnostics.EnumerateArray())
        {
            var id = d.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "?" : "?";
            var sev = d.TryGetProperty("severity", out var sevProp) ? sevProp.GetString() ?? "?" : "?";
            var msg = d.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";

            severityCounts[sev] = severityCounts.GetValueOrDefault(sev) + 1;

            var shortPath = "";
            var line = 0;
            if (d.TryGetProperty("location", out var loc))
            {
                if (loc.TryGetProperty("filePath", out var fp))
                {
                    var fullPath = fp.GetString() ?? "";
                    shortPath = fullPath.Length > commonPrefix.Length
                        ? fullPath[commonPrefix.Length..]
                        : Path.GetFileName(fullPath);
                }
                if (loc.TryGetProperty("startLine", out var sl))
                    line = sl.GetInt32();
            }

            var location = line > 0 ? $"{shortPath}:{line}" : shortPath;

            // Group key: id + truncated message (same ID can have different messages with different type names)
            var msgKey = msg.Length > 80 ? msg[..80] : msg;
            var groupKey = $"{id}|{msgKey}";

            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = (msg.Length > 200 ? msg[..200] + "..." : msg, sev, new List<string>());
                groups[groupKey] = group;
            }
            group.Locations.Add(location);
        }

        var sb = new StringBuilder();

        // Summary line
        var sevParts = severityCounts.OrderByDescending(kv => kv.Key.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(kv => $"{kv.Value} {kv.Key}");
        sb.AppendLine($"Total: {total} diagnostics ({string.Join(", ", sevParts)})");
        sb.AppendLine();

        // Output grouped diagnostics, errors first
        var orderedGroups = groups.OrderBy(g => g.Value.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(g => g.Value.Locations.Count);

        foreach (var (key, group) in orderedGroups)
        {
            var id = key.Split('|')[0];
            var countLabel = group.Locations.Count > 1 ? $" x{group.Locations.Count}" : "";
            sb.AppendLine($"[{group.Severity}] {id}{countLabel}: {group.Message}");

            // Show up to 10 locations per group, compact
            var locs = group.Locations.Take(10).ToList();
            sb.AppendLine($"  {string.Join(", ", locs)}");
            if (group.Locations.Count > 10)
                sb.AppendLine($"  ...and {group.Locations.Count - 10} more");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FindCommonPathPrefix(List<string> paths)
    {
        if (paths.Count == 0) return "";

        // Normalize to backslash
        var normalized = paths.Select(p => p.Replace('/', '\\')).ToList();
        var first = normalized[0];

        var prefixLen = first.Length;
        foreach (var path in normalized.Skip(1))
        {
            prefixLen = Math.Min(prefixLen, path.Length);
            for (var i = 0; i < prefixLen; i++)
            {
                if (char.ToLowerInvariant(first[i]) != char.ToLowerInvariant(path[i]))
                {
                    prefixLen = i;
                    break;
                }
            }
        }

        // Trim to last path separator
        var prefix = first[..prefixLen];
        var lastSep = prefix.LastIndexOf('\\');
        return lastSep >= 0 ? prefix[..(lastSep + 1)] : "";
    }

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
