using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    public class RefactoringService : BaseRoslynService
    {
        public RefactoringService(AsyncPackage package, IWorkspaceProvider workspaceProvider)
            : base(package, workspaceProvider)
        {
        }

        public async Task<QueryResponse> FormatDocumentAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return new QueryResponse { Success = false, Error = "FilePath is required" };
            }

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return new QueryResponse { Success = false, Error = "Document not found" };
            }

            var formattedDocument = await Formatter.FormatAsync(document);
            var text = await formattedDocument.GetTextAsync();

            // Apply the changes to the workspace
            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (Workspace != null && Workspace.TryApplyChanges(formattedDocument.Project.Solution))
            {
                return new QueryResponse
                {
                    Success = true,
                    Message = "Document formatted successfully",
                    Data = new DocumentChangeInfo
                    {
                        FilePath = request.FilePath,
                        NewText = text.ToString()
                    }
                };
            }

            return new QueryResponse { Success = false, Error = "Failed to apply formatting changes" };
        }

        public async Task<QueryResponse> OrganizeUsingsAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return new QueryResponse { Success = false, Error = "FilePath is required" };
            }

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return new QueryResponse { Success = false, Error = "Document not found" };
            }

            var root = await document.GetSyntaxRootAsync();
            if (root == null)
            {
                return new QueryResponse { Success = false, Error = "Could not get syntax root" };
            }

            // Remove unused usings and sort
            var newRoot = root;

            // Get all using directives
            var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
            if (usings.Any())
            {
                // Sort usings alphabetically
                var sortedUsings = usings.OrderBy(u => u.Name?.ToString()).ToList();

                // Replace them in order
                for (int i = 0; i < usings.Count; i++)
                {
                    newRoot = newRoot.ReplaceNode(usings[i], sortedUsings[i]);
                }
            }

            var newDocument = document.WithSyntaxRoot(newRoot);
            var formattedDocument = await Formatter.FormatAsync(newDocument);

            await Package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (Workspace != null && Workspace.TryApplyChanges(formattedDocument.Project.Solution))
            {
                var text = await formattedDocument.GetTextAsync();
                return new QueryResponse
                {
                    Success = true,
                    Message = "Usings organized successfully",
                    Data = new DocumentChangeInfo
                    {
                        FilePath = request.FilePath,
                        NewText = text.ToString()
                    }
                };
            }

            return new QueryResponse { Success = false, Error = "Failed to organize usings" };
        }

        public async Task<QueryResponse> RenameSymbolAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath) || !request.Line.HasValue || !request.Column.HasValue)
            {
                return new QueryResponse { Success = false, Error = "FilePath, Line, and Column are required" };
            }

            string? newName = null;
            request.Parameters?.TryGetValue("newName", out newName);

            if (string.IsNullOrEmpty(newName))
            {
                return new QueryResponse { Success = false, Error = "newName parameter is required" };
            }

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return new QueryResponse { Success = false, Error = "Document not found" };
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
            var node = syntaxRoot?.FindToken(position).Parent;
            var symbol = semanticModel?.GetSymbolInfo(node).Symbol;

            if (symbol == null)
            {
                return new QueryResponse { Success = false, Error = "Symbol not found at the specified location" };
            }

            try
            {
                var newSolution = await Renamer.RenameSymbolAsync(
                    document.Project.Solution,
                    symbol,
                    newName,
                    document.Project.Solution.Workspace.Options
                );

                var changes = newSolution.GetChanges(document.Project.Solution);
                var changedDocs = new List<DocumentChangeInfo>();
                int totalChanges = 0;

                foreach (var projectChanges in changes.GetProjectChanges())
                {
                    foreach (var changedDocId in projectChanges.GetChangedDocuments())
                    {
                        var oldDoc = document.Project.Solution.GetDocument(changedDocId);
                        var newDoc = newSolution.GetDocument(changedDocId);

                        if (oldDoc != null && newDoc != null)
                        {
                            var newText = await newDoc.GetTextAsync();
                            changedDocs.Add(new DocumentChangeInfo
                            {
                                FilePath = newDoc.FilePath,
                                NewText = newText.ToString()
                            });
                            totalChanges++;
                        }
                    }
                }

                await Package.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (Workspace != null && Workspace.TryApplyChanges(newSolution))
                {
                    return new QueryResponse
                    {
                        Success = true,
                        Message = $"Renamed '{symbol.Name}' to '{newName}' in {totalChanges} document(s)",
                        Data = new RenameResult
                        {
                            ChangedDocuments = changedDocs,
                            TotalChanges = totalChanges
                        }
                    };
                }

                return new QueryResponse { Success = false, Error = "Failed to apply rename changes" };
            }
            catch (Exception ex)
            {
                return new QueryResponse { Success = false, Error = $"Rename failed: {ex.Message}" };
            }
        }

        public async Task<QueryResponse> AddMissingUsingAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath) || !request.Line.HasValue || !request.Column.HasValue)
            {
                return new QueryResponse { Success = false, Error = "FilePath, Line, and Column are required" };
            }

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return new QueryResponse { Success = false, Error = "Document not found" };
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
            var node = syntaxRoot?.FindToken(position).Parent;

            if (node == null || semanticModel == null)
            {
                return new QueryResponse { Success = false, Error = "Could not analyze position" };
            }

            // Get the symbol info
            var symbolInfo = semanticModel.GetSymbolInfo(node);
            if (symbolInfo.Symbol != null)
            {
                return new QueryResponse { Success = false, Error = "Symbol is already resolved" };
            }

            // Try to find the type in other namespaces
            var compilation = semanticModel.Compilation;
            var typeName = node.ToString();

            INamedTypeSymbol? foundType = null;
            foreach (var assembly in compilation.References)
            {
                var assemblySymbol = compilation.GetAssemblyOrModuleSymbol(assembly) as IAssemblySymbol;
                if (assemblySymbol != null)
                {
                    foundType = FindTypeInAssembly(assemblySymbol.GlobalNamespace, typeName);
                    if (foundType != null) break;
                }
            }

            // Also search in current compilation
            if (foundType == null)
            {
                foundType = FindTypeInAssembly(compilation.GlobalNamespace, typeName);
            }

            if (foundType != null && syntaxRoot != null)
            {
                var namespaceToAdd = foundType.ContainingNamespace.ToDisplayString();
                var compilationUnit = syntaxRoot as CompilationUnitSyntax;

                if (compilationUnit != null)
                {
                    var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceToAdd))
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    var newCompilationUnit = compilationUnit.AddUsings(usingDirective);
                    var newDocument = document.WithSyntaxRoot(newCompilationUnit);
                    var formattedDocument = await Formatter.FormatAsync(newDocument);

                    await Package.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (Workspace != null && Workspace.TryApplyChanges(formattedDocument.Project.Solution))
                    {
                        return new QueryResponse
                        {
                            Success = true,
                            Message = $"Added using {namespaceToAdd};"
                        };
                    }
                }
            }

            return new QueryResponse { Success = false, Error = "Could not find a suitable using directive to add" };
        }

        public async Task<QueryResponse> ApplyCodeFixAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath) || !request.Line.HasValue || !request.Column.HasValue)
            {
                return new QueryResponse { Success = false, Error = "FilePath, Line, and Column are required" };
            }

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return new QueryResponse { Success = false, Error = "Document not found" };
            }

            // Get diagnostics at the location
            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return new QueryResponse { Success = false, Error = "Could not get semantic model" };
            }

            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
            var diagnostics = semanticModel.GetDiagnostics();

            // Find diagnostics at the position
            var relevantDiagnostics = diagnostics
                .Where(d => d.Location.SourceSpan.Contains(position))
                .ToList();

            if (!relevantDiagnostics.Any())
            {
                return new QueryResponse { Success = false, Error = "No diagnostics found at the specified location" };
            }

            // For now, just return the available diagnostics
            // Full code fix implementation would require loading all code fix providers
            var diagnosticInfos = relevantDiagnostics.Select(d => new CodeFixInfo
            {
                DiagnosticId = d.Id,
                Title = d.GetMessage(),
                Description = d.Descriptor.Description.ToString()
            }).ToList();

            return new QueryResponse
            {
                Success = true,
                Message = $"Found {diagnosticInfos.Count} diagnostic(s) at location",
                Data = diagnosticInfos
            };
        }
    }
}
