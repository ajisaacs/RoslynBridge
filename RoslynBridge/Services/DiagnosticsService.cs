#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    public class DiagnosticsService : BaseRoslynService
    {
        public DiagnosticsService(AsyncPackage package, IWorkspaceProvider workspaceProvider)
            : base(package, workspaceProvider)
        {
        }

        public async Task<QueryResponse> GetDiagnosticsAsync(QueryRequest request)
        {
            var diagnostics = new List<DiagnosticInfo>();

            if (!string.IsNullOrEmpty(request.FilePath))
            {
                var document = Workspace?.CurrentSolution.Projects
                    .SelectMany(p => p.Documents)
                    .FirstOrDefault(d => d.FilePath?.Equals(request.FilePath, StringComparison.OrdinalIgnoreCase) == true);

                if (document != null)
                {
                    if (IsGeneratedFile(document.FilePath))
                    {
                        return new QueryResponse { Success = true, Data = diagnostics };
                    }
                    var semanticModel = await document.GetSemanticModelAsync();
                    if (semanticModel != null)
                    {
                        var diags = semanticModel.GetDiagnostics()
                            .Where(d => !IsFromGeneratedFile(d));
                        diagnostics.AddRange(diags.Select(d => CreateDiagnosticInfo(d)));
                    }
                }
            }
            else
            {
                foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation != null)
                    {
                        var diags = compilation.GetDiagnostics()
                            .Where(d => !IsFromGeneratedFile(d));
                        diagnostics.AddRange(diags.Select(d => CreateDiagnosticInfo(d)));
                    }
                }
            }

            return new QueryResponse { Success = true, Data = diagnostics };
        }

        private static bool IsFromGeneratedFile(Diagnostic diagnostic)
        {
            if (!diagnostic.Location.IsInSource) return false;
            var path = diagnostic.Location.SourceTree?.FilePath;
            return IsGeneratedFile(path);
        }

        private static bool IsGeneratedFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            try
            {
                var p = filePath.Replace('/', '\\').ToLowerInvariant();
                if (p.Contains("\\obj\\") || p.Contains("\\bin\\")) return true;
                if (p.EndsWith(".g.cs") || p.EndsWith(".g.i.cs") || p.EndsWith(".generated.cs") || p.EndsWith(".designer.cs")) return true;
                if (p.EndsWith("\\globalusings.g.cs")) return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<QueryResponse> FindReferencesAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath) || !request.Line.HasValue || !request.Column.HasValue)
            {
                return new QueryResponse { Success = false, Error = "FilePath, Line, and Column are required" };
            }

            // Use shared path resolution to reliably locate documents
            var document = FindDocument(request.FilePath!);

            if (document == null)
            {
                return new QueryResponse { Success = false, Error = "Document not found" };
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();

            if (semanticModel == null || syntaxRoot == null)
            {
                return new QueryResponse { Success = false, Error = "Could not get semantic model" };
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
            var node = syntaxRoot.FindToken(position).Parent;

            if (node == null)
            {
                return new QueryResponse { Success = false, Error = "Syntax node not found at specified position" };
            }

            var symbol = semanticModel.GetSymbolInfo(node).Symbol;

            // Fallback: if positioned on a declaration (class, method, etc.),
            // resolve via GetDeclaredSymbol walking up the ancestor chain.
            if (symbol == null)
            {
                var declNode = node;
                while (declNode != null && symbol == null)
                {
                    symbol = semanticModel.GetDeclaredSymbol(declNode);
                    declNode = declNode.Parent;
                }
            }

            if (symbol == null)
            {
                return new QueryResponse { Success = false, Error = "Symbol not found" };
            }

            if (Workspace?.CurrentSolution == null)
            {
                return new QueryResponse { Success = false, Error = "Workspace not available" };
            }

            var references = await SymbolFinder.FindReferencesAsync(symbol, Workspace.CurrentSolution);
            var locations = references.SelectMany(r => r.Locations)
                .Select(loc => new LocationInfo
                {
                    FilePath = loc.Document.FilePath,
                    StartLine = loc.Location.GetLineSpan().StartLinePosition.Line + 1,
                    StartColumn = loc.Location.GetLineSpan().StartLinePosition.Character,
                    EndLine = loc.Location.GetLineSpan().EndLinePosition.Line + 1,
                    EndColumn = loc.Location.GetLineSpan().EndLinePosition.Character
                }).ToList();

            return new QueryResponse { Success = true, Data = locations };
    }
}
}
