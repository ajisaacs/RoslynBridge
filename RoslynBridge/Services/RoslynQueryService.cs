#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Constants;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    /// <summary>
    /// Main orchestrator service that delegates queries to specialized services
    /// </summary>
    public class RoslynQueryService : IRoslynQueryService
    {
        private readonly AsyncPackage _package;
        private readonly IWorkspaceProvider _workspaceProvider;
        private readonly SymbolQueryService _symbolService;
        private readonly DocumentQueryService _documentService;
        private readonly DiagnosticsService _diagnosticsService;
        private readonly RefactoringService _refactoringService;
        private readonly ProjectOperationsService _projectOperationsService;
        private readonly CodeSmellAnalysisService _codeSmellService;

        public RoslynQueryService(AsyncPackage package)
        {
            _package = package;
            _workspaceProvider = new WorkspaceProvider(package);

            // Initialize specialized services
            _symbolService = new SymbolQueryService(package, _workspaceProvider);
            _documentService = new DocumentQueryService(package, _workspaceProvider);
            _diagnosticsService = new DiagnosticsService(package, _workspaceProvider);
            _refactoringService = new RefactoringService(package, _workspaceProvider);
            _projectOperationsService = new ProjectOperationsService(_workspaceProvider);
            _codeSmellService = new CodeSmellAnalysisService(package, _workspaceProvider);
        }

        public async Task InitializeAsync()
        {
            await ((WorkspaceProvider)_workspaceProvider).InitializeAsync();
        }

        public async Task<QueryResponse> ExecuteQueryAsync(QueryRequest request)
        {
            try
            {
                // Handle project operations (don't require workspace)
                var queryType = request.QueryType?.ToLowerInvariant();
                switch (queryType)
                {
                    case QueryTypes.AddNuGetPackage:
                        return await _projectOperationsService.AddNuGetPackageAsync(request.ProjectName ?? string.Empty, request.PackageName ?? string.Empty, request.Version);
                    case QueryTypes.RemoveNuGetPackage:
                        return await _projectOperationsService.RemoveNuGetPackageAsync(request.ProjectName ?? string.Empty, request.PackageName ?? string.Empty);
                    case QueryTypes.BuildProject:
                        return await _projectOperationsService.BuildAsync(request.ProjectName ?? string.Empty, request.Configuration);
                    case QueryTypes.CleanProject:
                        return await _projectOperationsService.CleanAsync(request.ProjectName ?? string.Empty);
                    case QueryTypes.RestorePackages:
                        return await _projectOperationsService.RestoreAsync(request.ProjectName ?? string.Empty);
                    case QueryTypes.CreateDirectory:
                        return await _projectOperationsService.CreateDirectoryAsync(request.DirectoryPath ?? string.Empty);
                }

                // For Roslyn queries, ensure workspace is initialized
                if (_workspaceProvider.Workspace == null)
                {
                    await InitializeAsync();
                }

                if (_workspaceProvider.Workspace?.CurrentSolution == null)
                {
                    return new QueryResponse
                    {
                        Success = false,
                        Error = "No solution is currently open"
                    };
                }

                return request.QueryType?.ToLowerInvariant() switch
                {
                    // Symbol queries
                    QueryTypes.GetSymbol => await _symbolService.GetSymbolInfoAsync(request),
                    QueryTypes.FindSymbol => await _symbolService.FindSymbolAsync(request),
                    QueryTypes.GetTypeMembers => await _symbolService.GetTypeMembersAsync(request),
                    QueryTypes.GetTypeHierarchy => await _symbolService.GetTypeHierarchyAsync(request),
                    QueryTypes.FindImplementations => await _symbolService.FindImplementationsAsync(request),
                    QueryTypes.GetCallHierarchy => await _symbolService.GetCallHierarchyAsync(request),
                    QueryTypes.GetSymbolContext => await _symbolService.GetSymbolContextAsync(request),

                    // Document queries
                    QueryTypes.GetDocument => await _documentService.GetDocumentInfoAsync(request),
                    QueryTypes.GetProjects => await _documentService.GetProjectsAsync(request),
                    QueryTypes.GetSemanticModel => await _documentService.GetSemanticModelAsync(request),
                    QueryTypes.GetSyntaxTree => await _documentService.GetSyntaxTreeAsync(request),
                    QueryTypes.GetSolutionOverview => await _documentService.GetSolutionOverviewAsync(request),
                    QueryTypes.GetNamespaceTypes => await _documentService.GetNamespaceTypesAsync(request),
                    QueryTypes.SearchCode => await _documentService.SearchCodeAsync(request),

                    // Diagnostics queries
                    QueryTypes.GetDiagnostics => await _diagnosticsService.GetDiagnosticsAsync(request),
                    QueryTypes.FindReferences => await _diagnosticsService.FindReferencesAsync(request),

                    // Refactoring operations
                    QueryTypes.ApplyCodeFix => await _refactoringService.ApplyCodeFixAsync(request),
                    QueryTypes.FormatDocument => await _refactoringService.FormatDocumentAsync(request),
                    QueryTypes.RenameSymbol => await _refactoringService.RenameSymbolAsync(request),
                    QueryTypes.OrganizeUsings => await _refactoringService.OrganizeUsingsAsync(request),
                    QueryTypes.AddMissingUsing => await _refactoringService.AddMissingUsingAsync(request),

                    // Code smell analysis
                    QueryTypes.GetCodeSmells => await _codeSmellService.GetCodeSmellsAsync(request),
                    QueryTypes.GetCodeSmellSummary => await _codeSmellService.GetCodeSmellSummaryAsync(request),

                    _ => new QueryResponse
                    {
                        Success = false,
                        Error = $"Unknown query type: {request.QueryType}"
                    }
                };
            }
            catch (Exception ex)
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
    }
}
