#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;
using System.Threading.Tasks;

namespace RoslynBridge.Services
{
    public abstract class BaseRoslynService
    {
        protected readonly AsyncPackage Package;
        protected readonly IWorkspaceProvider WorkspaceProvider;

        protected VisualStudioWorkspace? Workspace => WorkspaceProvider.Workspace;

        protected BaseRoslynService(AsyncPackage package, IWorkspaceProvider workspaceProvider)
        {
            Package = package;
            WorkspaceProvider = workspaceProvider;
        }

        protected async Task<RoslynBridge.Models.SymbolInfo> CreateSymbolInfoAsync(ISymbol symbol)
        {
            var locations = symbol.Locations.Where(loc => loc.IsInSource)
                .Select(loc => new LocationInfo
                {
                    FilePath = loc.SourceTree?.FilePath,
                    StartLine = loc.GetLineSpan().StartLinePosition.Line + 1,
                    StartColumn = loc.GetLineSpan().StartLinePosition.Character,
                    EndLine = loc.GetLineSpan().EndLinePosition.Line + 1,
                    EndColumn = loc.GetLineSpan().EndLinePosition.Character
                }).ToList();

            return await Task.FromResult(new RoslynBridge.Models.SymbolInfo
            {
                Name = symbol.Name,
                Kind = symbol.Kind.ToString(),
                Type = (symbol as ITypeSymbol)?.ToDisplayString(),
                ContainingType = symbol.ContainingType?.Name,
                ContainingNamespace = symbol.ContainingNamespace?.ToDisplayString(),
                Locations = locations,
                Documentation = symbol.GetDocumentationCommentXml(),
                Modifiers = symbol.DeclaringSyntaxReferences.Length > 0
                    ? symbol.DeclaringSyntaxReferences[0].GetSyntax().ChildTokens()
                        .Where(t => SyntaxFacts.IsKeywordKind(t.Kind()))
                        .Select(t => t.Text)
                        .ToList()
                    : new List<string>()
            });
        }

        protected DiagnosticInfo CreateDiagnosticInfo(Diagnostic diagnostic)
        {
            var location = diagnostic.Location.GetLineSpan();
            return new DiagnosticInfo
            {
                Id = diagnostic.Id,
                Severity = diagnostic.Severity.ToString(),
                Message = diagnostic.GetMessage(),
                Location = new LocationInfo
                {
                    FilePath = location.Path,
                    StartLine = location.StartLinePosition.Line + 1,
                    StartColumn = location.StartLinePosition.Character,
                    EndLine = location.EndLinePosition.Line + 1,
                    EndColumn = location.EndLinePosition.Character
                }
            };
        }

        protected List<string> GetModifiers(ISymbol symbol)
        {
            var modifiers = new List<string>();

            if (symbol.IsStatic) modifiers.Add("static");
            if (symbol.IsAbstract) modifiers.Add("abstract");
            if (symbol.IsVirtual) modifiers.Add("virtual");
            if (symbol.IsOverride) modifiers.Add("override");
            if (symbol.IsSealed) modifiers.Add("sealed");

            modifiers.Add(symbol.DeclaredAccessibility.ToString().ToLower());

            return modifiers;
        }

        protected string? ExtractSummary(string? xmlDoc)
        {
            if (string.IsNullOrEmpty(xmlDoc)) return null;

            // At this point xmlDoc is guaranteed to be non-null
            var summaryStart = xmlDoc!.IndexOf("<summary>", StringComparison.Ordinal);
            var summaryEnd = xmlDoc.IndexOf("</summary>", StringComparison.Ordinal);

            if (summaryStart >= 0 && summaryEnd > summaryStart)
            {
                return xmlDoc.Substring(summaryStart + 9, summaryEnd - summaryStart - 9).Trim();
            }

            return null;
        }

        protected IEnumerable<INamespaceSymbol> GetNamespaces(INamespaceSymbol root)
        {
            yield return root;
            foreach (var child in root.GetNamespaceMembers())
            {
                foreach (var ns in GetNamespaces(child))
                {
                    yield return ns;
                }
            }
        }

        protected INamedTypeSymbol? FindTypeInAssembly(INamespaceSymbol namespaceSymbol, string typeName)
        {
            var types = namespaceSymbol.GetTypeMembers(typeName);
            if (types.Any())
            {
                return types.First();
            }

            foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                var result = FindTypeInAssembly(childNamespace, typeName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        protected Document? FindDocument(string filePath)
        {
            var solution = Workspace?.CurrentSolution;
            if (solution == null) return null;

            var ids = solution.GetDocumentIdsWithFilePath(filePath);
            if (ids != null && ids.Any())
            {
                return solution.GetDocument(ids[0]);
            }

            var normalizedTarget = SafeFullPath(filePath);
            var byFullPath = solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => SafeFullPath(d.FilePath) == normalizedTarget);
            if (byFullPath != null) return byFullPath;

            var fileName = Path.GetFileName(filePath);
            return solution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath != null &&
                                     Path.GetFileName(d.FilePath).Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeFullPath(string? path)
        {
            try { return Path.GetFullPath(path ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant(); }
            catch { return (path ?? string.Empty).Replace('/', '\\').TrimEnd('\\').ToLowerInvariant(); }
        }

        protected QueryResponse CreateErrorResponse(string errorMessage)
        {
            return new QueryResponse { Success = false, Error = errorMessage };
        }

        protected QueryResponse CreateSuccessResponse(object? data = null, string? message = null)
        {
            return new QueryResponse
            {
                Success = true,
                Data = data,
                Message = message
            };
        }
    }
}
