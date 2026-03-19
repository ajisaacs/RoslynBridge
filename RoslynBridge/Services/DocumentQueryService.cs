#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.IO;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    public class DocumentQueryService : BaseRoslynService
    {
        public DocumentQueryService(AsyncPackage package, IWorkspaceProvider workspaceProvider)
            : base(package, workspaceProvider)
        {
        }

        public async Task<QueryResponse> GetDocumentInfoAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return CreateErrorResponse("FilePath is required");
            }

            // FilePath is guaranteed to be non-null after the check above
            var document = FindDocument(request.FilePath!);

            if (document == null)
            {
                return CreateErrorResponse("Document not found");
            }

            var syntaxRoot = await document.GetSyntaxRootAsync();
            if (syntaxRoot == null)
            {
                return CreateErrorResponse("Could not get syntax tree");
            }

            var docInfo = new Models.DocumentInfo
            {
                FilePath = document.FilePath,
                Name = document.Name,
                ProjectName = document.Project.Name,
                Usings = syntaxRoot.DescendantNodes().OfType<UsingDirectiveSyntax>()
                    .Select(u => u.Name?.ToString() ?? string.Empty).ToList(),
                Classes = syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Select(c => c.Identifier.Text).ToList(),
                Interfaces = syntaxRoot.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
                    .Select(i => i.Identifier.Text).ToList(),
                Enums = syntaxRoot.DescendantNodes().OfType<EnumDeclarationSyntax>()
                    .Select(e => e.Identifier.Text).ToList()
            };

            return CreateSuccessResponse(docInfo);
        }

        public async Task<QueryResponse> GetProjectsAsync(QueryRequest request)
        {
            await Task.CompletedTask;

            var projects = Workspace?.CurrentSolution.Projects.Select(p => new Models.ProjectInfo
            {
                Name = p.Name,
                FilePath = p.FilePath,
                Documents = p.Documents.Select(d => d.FilePath ?? string.Empty).ToList(),
                References = p.MetadataReferences.Select(r => r.Display ?? string.Empty).ToList()
            }).ToList();

            return CreateSuccessResponse(projects);
        }

        public async Task<QueryResponse> GetFilesAsync(QueryRequest request)
        {
            await Task.CompletedTask;

            // Get the project name filter if provided
            string? projectNameFilter = request.ProjectName;

            // Get path and pattern filters from parameters
            string? pathFilter = null;
            string? patternFilter = null;
            request.Parameters?.TryGetValue("path", out pathFilter);
            request.Parameters?.TryGetValue("pattern", out patternFilter);

            var projects = Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>();

            // Filter by project name if provided
            if (!string.IsNullOrEmpty(projectNameFilter))
            {
                projects = projects.Where(p => p.Name == projectNameFilter);
            }

            // Get all documents from the filtered projects
            var allDocuments = projects.SelectMany(p => p.Documents);

            // Apply path filter if provided
            if (!string.IsNullOrEmpty(pathFilter))
            {
                // Normalize path separators to forward slashes for cross-platform compatibility
                var normalizedFilter = pathFilter.Replace('\\', '/').ToLower();

                allDocuments = allDocuments.Where(d =>
                {
                    if (d.FilePath == null) return false;
                    var normalizedPath = d.FilePath.Replace('\\', '/').ToLower();
                    return normalizedPath.Contains(normalizedFilter);
                });
            }

            // Apply pattern filter (glob-style) if provided
            if (!string.IsNullOrEmpty(patternFilter))
            {
                allDocuments = allDocuments.Where(d =>
                {
                    if (d.FilePath == null) return false;
                    var fileName = System.IO.Path.GetFileName(d.FilePath);
                    return MatchesGlobPattern(fileName, patternFilter);
                });
            }

            var filePaths = allDocuments.Select(d => d.FilePath ?? string.Empty).ToList();

            return CreateSuccessResponse(filePaths);
        }

        private bool MatchesGlobPattern(string fileName, string pattern)
        {
            // Convert glob pattern to regex
            // * matches any sequence of characters
            // ? matches any single character
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return System.Text.RegularExpressions.Regex.IsMatch(
                fileName,
                regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        public async Task<QueryResponse> GetSyntaxTreeAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return CreateErrorResponse("FilePath is required");
            }

            var document = FindDocument(request.FilePath!);

            if (document == null)
            {
                return CreateErrorResponse("Document not found");
            }

            var syntaxTree = await document.GetSyntaxTreeAsync();
            if (syntaxTree == null)
            {
                return CreateErrorResponse("Could not get syntax tree");
            }

            var root = await syntaxTree.GetRootAsync();
            return CreateSuccessResponse(new
            {
                FilePath = syntaxTree.FilePath,
                Text = root.ToFullString()
            });
        }

        public async Task<QueryResponse> GetSemanticModelAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return CreateErrorResponse("FilePath is required");
            }

            var document = FindDocument(request.FilePath!);

            if (document == null)
            {
                return CreateErrorResponse("Document not found");
            }

            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return CreateErrorResponse("Could not get semantic model");
            }

            return CreateSuccessResponse(message: "Semantic model retrieved successfully (not serializable, use specific queries)");
        }

        public async Task<QueryResponse> GetSolutionOverviewAsync(QueryRequest request)
        {
            var projects = Workspace?.CurrentSolution.Projects.ToList() ?? new List<Project>();
            var overview = new SolutionOverview
            {
                ProjectCount = projects.Count,
                DocumentCount = projects.Sum(p => p.Documents.Count()),
                TopLevelNamespaces = new List<string>(),
                Projects = new List<ProjectSummary>()
            };

            var allNamespaces = new HashSet<string>();

            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var projectNamespaces = new HashSet<string>();
                var globalNamespace = compilation.GlobalNamespace;

                foreach (var ns in GetNamespaces(globalNamespace))
                {
                    var nsName = ns.ToDisplayString();
                    if (!string.IsNullOrEmpty(nsName))
                    {
                        allNamespaces.Add(nsName.Split('.').First());
                        projectNamespaces.Add(nsName);
                    }
                }

                overview.Projects.Add(new ProjectSummary
                {
                    Name = project.Name,
                    FileCount = project.Documents.Count(),
                    TopNamespaces = projectNamespaces.Take(10).ToList()
                });
            }

            overview.TopLevelNamespaces = allNamespaces.ToList();

            return CreateSuccessResponse(overview);
        }

        public async Task<QueryResponse> GetNamespaceTypesAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("SymbolName (namespace name) is required");
            }

            var types = new List<NamespaceTypeInfo>();

            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var namespaceSymbol = compilation.GetSymbolsWithName(
                    name => name == request.SymbolName,
                    SymbolFilter.Namespace
                ).FirstOrDefault() as INamespaceSymbol;

                if (namespaceSymbol != null)
                {
                    var namespaceTypes = namespaceSymbol.GetTypeMembers();
                    foreach (var type in namespaceTypes)
                    {
                        types.Add(new NamespaceTypeInfo
                        {
                            Name = type.Name,
                            Kind = type.TypeKind.ToString(),
                            FullName = type.ToDisplayString(),
                            Summary = ExtractSummary(type.GetDocumentationCommentXml())
                        });
                    }

                    return CreateSuccessResponse(types);
                }
            }

            return CreateErrorResponse($"Namespace '{request.SymbolName}' not found");
        }

        public async Task<QueryResponse> SearchCodeAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("Search pattern is required");
            }

            string? scope = null;
            request.Parameters?.TryGetValue("scope", out scope);
            scope = scope ?? "all";

            string? projectFilter = null;
            request.Parameters?.TryGetValue("projectName", out projectFilter);

            // "text" mode searches actual source text; "symbols" mode searches symbol names (legacy behavior)
            string? mode = null;
            request.Parameters?.TryGetValue("mode", out mode);
            mode = mode ?? "text"; // default to text search since that's what users expect

            if (mode == "symbols")
            {
                return await SearchCodeBySymbolNameAsync(request.SymbolName, scope, projectFilter);
            }

            return await SearchCodeBySourceTextAsync(request.SymbolName, projectFilter);
        }

        private async Task<QueryResponse> SearchCodeBySymbolNameAsync(string pattern, string scope, string? projectFilter)
        {
            var results = new List<RoslynBridge.Models.SymbolInfo>();

            var projects = Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>();
            if (!string.IsNullOrEmpty(projectFilter))
            {
                projects = projects.Where(p => p.Name.Equals(projectFilter, System.StringComparison.OrdinalIgnoreCase));
            }

            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var symbols = compilation.GetSymbolsWithName(
                    name => System.Text.RegularExpressions.Regex.IsMatch(name, pattern),
                    SymbolFilter.All
                );

                foreach (var symbol in symbols)
                {
                    if (scope != "all")
                    {
                        var symbolKind = symbol.Kind.ToString().ToLowerInvariant();
                        if (scope == "methods" && symbolKind != "method") continue;
                        if (scope == "classes" && symbolKind != "namedtype") continue;
                        if (scope == "properties" && symbolKind != "property") continue;
                    }

                    results.Add(await CreateSymbolInfoAsync(symbol));
                }
            }

            return CreateSuccessResponse(results);
        }

        private async Task<QueryResponse> SearchCodeBySourceTextAsync(string pattern, string? projectFilter)
        {
            var results = new List<CodeSearchMatch>();
            System.Text.RegularExpressions.Regex regex;
            try
            {
                regex = new System.Text.RegularExpressions.Regex(pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.Compiled);
            }
            catch (System.ArgumentException ex)
            {
                return CreateErrorResponse($"Invalid regex pattern: {ex.Message}");
            }

            var projects = Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>();
            if (!string.IsNullOrEmpty(projectFilter))
            {
                projects = projects.Where(p => p.Name.Equals(projectFilter, System.StringComparison.OrdinalIgnoreCase));
            }

            const int maxResults = 200;

            foreach (var project in projects)
            {
                foreach (var document in project.Documents)
                {
                    var filePath = document.FilePath;
                    if (string.IsNullOrEmpty(filePath)) continue;

                    // Skip generated files
                    var lowerPath = filePath.Replace('/', '\\').ToLowerInvariant();
                    if (lowerPath.Contains("\\obj\\") || lowerPath.Contains("\\bin\\") ||
                        lowerPath.EndsWith(".designer.cs") || lowerPath.EndsWith(".generated.cs") ||
                        lowerPath.EndsWith(".g.cs") || lowerPath.EndsWith(".g.i.cs"))
                    {
                        continue;
                    }

                    var sourceText = await document.GetTextAsync();
                    if (sourceText == null) continue;

                    for (int i = 0; i < sourceText.Lines.Count; i++)
                    {
                        var lineText = sourceText.Lines[i].ToString();
                        if (regex.IsMatch(lineText))
                        {
                            results.Add(new CodeSearchMatch
                            {
                                FilePath = filePath,
                                ProjectName = project.Name,
                                Line = i + 1,
                                Text = lineText.Trim(),
                            });

                            if (results.Count >= maxResults)
                            {
                                return CreateSuccessResponse(new
                                {
                                    matches = results,
                                    truncated = true,
                                    message = $"Results capped at {maxResults}. Use projectName filter to narrow search."
                                });
                            }
                        }
                    }
                }
            }

            return CreateSuccessResponse(new
            {
                matches = results,
                truncated = false,
                message = $"Found {results.Count} match(es)"
            });
        }
    }
}
