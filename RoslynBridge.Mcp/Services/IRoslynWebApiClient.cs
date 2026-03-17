using System.Text.Json;

namespace RoslynBridge.Mcp.Services;

/// <summary>
/// Interface for communicating with the RoslynBridge WebAPI
/// </summary>
public interface IRoslynWebApiClient
{
    // Diagnostics
    Task<JsonDocument> GetDiagnosticsAsync(string? filePath = null, string? severity = null, int? limit = null, int? offset = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetDiagnosticsSummaryAsync(string? filePath = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetDiagnosticsCountAsync(string? filePath = null, string? severity = null, CancellationToken ct = default);

    // Symbols
    Task<JsonDocument> GetSymbolAsync(string filePath, int line, int column, string? fields = null, CancellationToken ct = default);
    Task<JsonDocument> FindReferencesAsync(string filePath, int line, int column, string? fields = null, CancellationToken ct = default);
    Task<JsonDocument> GetReferencesCountAsync(string filePath, int line, int column, CancellationToken ct = default);
    Task<JsonDocument> SearchSymbolAsync(string symbolName, string? kind = null, CancellationToken ct = default);

    // Projects
    Task<JsonDocument> GetProjectsAsync(string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetFilesAsync(string? projectName = null, string? path = null, string? pattern = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetSolutionOverviewAsync(CancellationToken ct = default);
    Task<JsonDocument> BuildProjectAsync(string projectName, string? configuration = null, CancellationToken ct = default);

    // Code Quality
    Task<JsonDocument> GetCodeSmellsAsync(string? filePath = null, string? projectName = null, string? smellType = null, string? severity = null, int? top = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetCodeSmellSummaryAsync(string? filePath = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetDuplicatesAsync(int? minLines = null, int? similarity = null, string? className = null, string? namespaceName = null, string? solutionName = null, CancellationToken ct = default);

    // Instances
    Task<JsonDocument> ListInstancesAsync(CancellationToken ct = default);
    Task<JsonDocument> GetInstanceBySolutionAsync(string solutionPath, CancellationToken ct = default);
    Task<JsonDocument> CheckHealthAsync(CancellationToken ct = default);

    // Workspace
    Task<JsonDocument> RefreshWorkspaceAsync(string? solutionName = null, string? filePath = null, CancellationToken ct = default);
    Task<JsonDocument> FormatDocumentAsync(string filePath, CancellationToken ct = default);

    // Advanced Symbol Operations (via query endpoint)
    Task<JsonDocument> GetTypeMembersAsync(string typeName, bool includeInherited = false, string? kind = null, string? accessibility = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetTypeHierarchyAsync(string typeName, string? direction = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> FindImplementationsAsync(string? symbolName = null, string? filePath = null, int? line = null, int? column = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetCallHierarchyAsync(string filePath, int line, int column, string? direction = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> SearchCodeAsync(string pattern, string? scope = null, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetSymbolContextAsync(string filePath, int line, int column, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetNamespaceTypesAsync(string namespaceName, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> GetSymbolSourceAsync(string symbolName, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> FindUsagesAsync(string symbolName, string? solutionName = null, CancellationToken ct = default);

    // Additional Project Operations (via query endpoint)
    Task<JsonDocument> CleanProjectAsync(string projectName, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> RestorePackagesAsync(string projectName, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> RemovePackageAsync(string projectName, string packageName, string? solutionName = null, CancellationToken ct = default);
    Task<JsonDocument> AddPackageAsync(string projectName, string packageName, string? version = null, string? solutionName = null, CancellationToken ct = default);
}
