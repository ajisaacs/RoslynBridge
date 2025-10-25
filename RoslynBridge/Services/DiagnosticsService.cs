using System;
using System.Collections.Generic;
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
                    var semanticModel = await document.GetSemanticModelAsync();
                    if (semanticModel != null)
                    {
                        var diags = semanticModel.GetDiagnostics();
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
                        var diags = compilation.GetDiagnostics();
                        diagnostics.AddRange(diags.Select(d => CreateDiagnosticInfo(d)));
                    }
                }
            }

            return new QueryResponse { Success = true, Data = diagnostics };
        }

        public async Task<QueryResponse> FindReferencesAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath) || !request.Line.HasValue || !request.Column.HasValue)
            {
                return new QueryResponse { Success = false, Error = "FilePath, Line, and Column are required" };
            }

            var document = Workspace?.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d => d.FilePath?.Equals(request.FilePath, StringComparison.OrdinalIgnoreCase) == true);

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
            var symbol = semanticModel.GetSymbolInfo(node).Symbol;

            if (symbol == null)
            {
                return new QueryResponse { Success = false, Error = "Symbol not found" };
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
