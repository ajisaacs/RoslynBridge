using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Services;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// MCP tools for project and solution operations
/// </summary>
[McpServerToolType]
public class ProjectTools
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
        return FormatResult(result);
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
        [Description("Optional solution name to target a specific VS instance")] string? solutionName = null,
        CancellationToken ct = default)
    {
        var result = await _client.GetFilesAsync(projectName, path, pattern, solutionName, ct);
        return FormatResult(result);
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
        return FormatResult(result);
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

    private static string FormatResult(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
    }
}
