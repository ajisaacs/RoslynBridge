using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for project and solution operations
/// </summary>
[McpServerToolType]
public partial class ProjectTools
{
    private readonly IRoslynWebApiClient _client;

    public ProjectTools(IRoslynWebApiClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Get all projects in the solution
    /// </summary>
    [McpServerTool(Name = "get_projects")]
    [Description("Get a list of all projects in the current solution with their metadata.")]
    public async Task<string> GetProjects(
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetProjectsAsync(solutionName, ct);
        return FormatProjectsCompact(result);
    }

    /// <summary>
    /// Get files from projects with optional filtering
    /// </summary>
    [McpServerTool(Name = "get_files")]
    [Description("Get files from projects with optional filtering by project name, path, or glob pattern.")]
    public async Task<string> GetFiles(
        [Description("Optional project name to filter files")] string? projectName = null,
        [Description("Optional path filter (case-insensitive contains match)")] string? path = null,
        [Description("Optional filename pattern (glob-style: * and ? wildcards)")] string? pattern = null,
        [Description("Max results to return. 0 = unlimited. Default 100.")] int limit = 100,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetFilesAsync(projectName, path, pattern, solutionName, ct);
        return ResultLimiter.LimitArrayResult(result, limit);
    }

    /// <summary>
    /// Get solution overview with statistics
    /// </summary>
    [McpServerTool(Name = "get_solution_overview")]
    [Description("Get a high-level overview of the solution including project count, file statistics, and overall structure.")]
    public async Task<string> GetSolutionOverview(CancellationToken ct = default)
    {
        var result = await _client.GetSolutionOverviewAsync(ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Build a project
    /// </summary>
    [McpServerTool(Name = "build_project")]
    [Description("Build a specific project in the solution. Returns build result with any errors or warnings.")]
    public async Task<string> BuildProject(
        [Description("Name of the project to build")] string projectName,
        [Description("Optional build configuration: Debug or Release")] string? configuration = null,
        CancellationToken ct = default)
    {
        var result = await _client.BuildProjectAsync(projectName, configuration, ct);
        return FormatBuildResultCompact(result);
    }

    /// <summary>
    /// Clean a project
    /// </summary>
    [McpServerTool(Name = "clean_project")]
    [Description("Clean a project, removing all build outputs (bin and obj folders).")]
    public async Task<string> CleanProject(
        [Description("Name of the project to clean")] string projectName,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.CleanProjectAsync(projectName, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Restore NuGet packages for a project
    /// </summary>
    [McpServerTool(Name = "restore_packages")]
    [Description("Restore NuGet packages for a project.")]
    public async Task<string> RestorePackages(
        [Description("Name of the project to restore packages for")] string projectName,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.RestorePackagesAsync(projectName, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Add a NuGet package to a project
    /// </summary>
    [McpServerTool(Name = "add_package")]
    [Description("Add a NuGet package to a project.")]
    public async Task<string> AddPackage(
        [Description("Name of the project to add the package to")] string projectName,
        [Description("NuGet package name (e.g., 'Newtonsoft.Json')")] string packageName,
        [Description("Optional specific version (e.g., '13.0.3'). If not specified, uses latest.")] string? version = null,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.AddPackageAsync(projectName, packageName, version, solutionName, ct);
        return FormatResult(result);
    }

    /// <summary>
    /// Remove a NuGet package from a project
    /// </summary>
    [McpServerTool(Name = "remove_package")]
    [Description("Remove a NuGet package from a project.")]
    public async Task<string> RemovePackage(
        [Description("Name of the project to remove the package from")] string projectName,
        [Description("NuGet package name to remove")] string packageName,
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.RemovePackageAsync(projectName, packageName, solutionName, ct);
        return FormatResult(result);
    }

    private static string FormatProjectsCompact(JsonDocument doc)
    {
        var root = doc.RootElement;

        if (root.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
        {
            var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error";
            return $"Error: {error}";
        }

        JsonElement projects;
        if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
            projects = dataProp;
        else if (root.ValueKind == JsonValueKind.Array)
            projects = root;
        else
            return FormatResult(doc);

        var count = projects.GetArrayLength();
        if (count == 0)
            return "No projects found.";

        var sb = new StringBuilder();
        sb.AppendLine($"{count} project(s)");
        sb.AppendLine();

        foreach (var proj in projects.EnumerateArray())
        {
            var name = proj.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
            var filePath = proj.TryGetProperty("filePath", out var fp) ? fp.GetString() ?? "" : "";
            var docCount = proj.TryGetProperty("documents", out var docs) && docs.ValueKind == JsonValueKind.Array
                ? docs.GetArrayLength() : 0;
            var refCount = proj.TryGetProperty("references", out var refs) && refs.ValueKind == JsonValueKind.Array
                ? refs.GetArrayLength() : 0;

            sb.AppendLine($"{name}");
            sb.AppendLine($"  Path: {filePath}");
            sb.AppendLine($"  Documents: {docCount}, References: {refCount}");
        }

        return sb.ToString().TrimEnd();
    }

    // Hard limit to stay well under MCP token limits
    private const int MaxOutputChars = 50_000;
    private const int MaxDiagnosticGroups = 40;

    private static string FormatBuildResultCompact(JsonDocument doc)
    {
        var root = doc.RootElement;

        // Check for error response from the API itself
        if (root.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
        {
            var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error";
            return $"Build failed: {error}";
        }

        // Extract build output fields from data
        if (!root.TryGetProperty("data", out var data))
            return FormatResult(doc);

        var exitCode = data.TryGetProperty("exitCode", out var ec) ? ec.GetInt32() : -1;
        var output = data.TryGetProperty("output", out var outProp) ? outProp.GetString() ?? "" : "";
        var stderr = data.TryGetProperty("error", out var errOutput) ? errOutput.GetString() ?? "" : "";
        var duration = data.TryGetProperty("duration", out var dur) ? dur.GetDouble() : 0;
        var buildSucceeded = exitCode == 0;

        var sb = new StringBuilder();
        sb.AppendLine(buildSucceeded ? "Build SUCCEEDED" : "Build FAILED");
        sb.AppendLine($"Duration: {duration:F0}ms");
        sb.AppendLine();

        // Parse MSBuild error/warning lines from output and stderr
        var combined = output + "\n" + stderr;
        var diagnosticLines = new List<(string Severity, string Code, string File, int Line, string Message)>();

        foreach (var line in combined.Split('\n'))
        {
            var match = MsBuildDiagnosticRegex().Match(line);
            if (match.Success)
            {
                var file = match.Groups["file"].Value;
                var lineNum = int.TryParse(match.Groups["line"].Value, out var ln) ? ln : 0;
                var severity = match.Groups["severity"].Value;
                var code = match.Groups["code"].Value;
                var message = match.Groups["message"].Value.Trim();
                diagnosticLines.Add((severity, code, file, lineNum, message));
            }
        }

        if (diagnosticLines.Count == 0)
        {
            // No parseable diagnostics — show a trimmed version of the output
            if (buildSucceeded)
            {
                sb.AppendLine("No errors or warnings.");
            }
            else
            {
                // Show last 50 lines of output as fallback
                var lines = combined.Split('\n')
                    .Select(l => l.TrimEnd())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();
                var start = Math.Max(0, lines.Length - 50);
                for (var i = start; i < lines.Length; i++)
                    sb.AppendLine(lines[i]);
            }
            return TruncateOutput(sb);
        }

        // Find common path prefix
        var allPaths = diagnosticLines.Select(d => d.File).Where(f => !string.IsNullOrEmpty(f)).ToList();
        var commonPrefix = FindCommonPathPrefix(allPaths);

        // Count by severity
        var errorCount = diagnosticLines.Count(d => d.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
        var warningCount = diagnosticLines.Count(d => d.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
        sb.AppendLine($"{errorCount} error(s), {warningCount} warning(s)");
        sb.AppendLine();

        // Group by code + message
        var groups = new Dictionary<string, (string Severity, string Message, List<string> Locations)>();
        foreach (var (severity, code, file, lineNum, message) in diagnosticLines)
        {
            var shortPath = file.Length > commonPrefix.Length
                ? file[commonPrefix.Length..]
                : Path.GetFileName(file);
            var location = lineNum > 0 ? $"{shortPath}:{lineNum}" : shortPath;

            var msgKey = message.Length > 80 ? message[..80] : message;
            var groupKey = $"{code}|{msgKey}";

            if (!groups.TryGetValue(groupKey, out var group))
            {
                group = (severity, message.Length > 200 ? message[..200] + "..." : message, new List<string>());
                groups[groupKey] = group;
            }
            group.Locations.Add(location);
        }

        // Output errors first, then warnings, sorted by count desc
        var ordered = groups
            .OrderBy(g => g.Value.Severity.Equals("error", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(g => g.Value.Locations.Count)
            .ToList();

        var shown = 0;
        foreach (var (key, group) in ordered)
        {
            if (shown >= MaxDiagnosticGroups)
            {
                var remaining = ordered.Count - shown;
                sb.AppendLine($"\n...and {remaining} more diagnostic group(s) omitted (use get_diagnostics for full details)");
                break;
            }

            var code = key.Split('|')[0];
            var countLabel = group.Locations.Count > 1 ? $" x{group.Locations.Count}" : "";
            sb.AppendLine($"[{group.Severity}] {code}{countLabel}: {group.Message}");

            var locs = group.Locations.Take(5).ToList();
            sb.AppendLine($"  {string.Join(", ", locs)}");
            if (group.Locations.Count > 5)
                sb.AppendLine($"  ...and {group.Locations.Count - 5} more location(s)");

            shown++;
        }

        return TruncateOutput(sb);
    }

    private static string TruncateOutput(StringBuilder sb)
    {
        var result = sb.ToString().TrimEnd();
        if (result.Length <= MaxOutputChars)
            return result;
        return result[..MaxOutputChars] + "\n\n...[output truncated at 50K chars]";
    }

    private static string FindCommonPathPrefix(List<string> paths)
    {
        if (paths.Count == 0) return "";

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

        var prefix = first[..prefixLen];
        var lastSep = prefix.LastIndexOf('\\');
        return lastSep >= 0 ? prefix[..(lastSep + 1)] : "";
    }

    // Matches MSBuild diagnostic lines like:
    // C:\path\file.cs(10,5): error CS0234: The type or namespace...
    // Also handles paths without column: file.cs(10): error CS0234: ...
    [GeneratedRegex(@"(?<file>[^\(]+)\((?<line>\d+)(?:,\d+)?\)\s*:\s*(?<severity>error|warning)\s+(?<code>\w+)\s*:\s*(?<message>.+)", RegexOptions.IgnoreCase)]
    private static partial Regex MsBuildDiagnosticRegex();

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
