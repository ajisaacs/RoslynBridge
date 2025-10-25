using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    public class SymbolQueryService : BaseRoslynService
    {
        public SymbolQueryService(AsyncPackage package, IWorkspaceProvider workspaceProvider)
            : base(package, workspaceProvider)
        {
        }

        public async Task<QueryResponse> GetSymbolInfoAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath))
            {
                return CreateErrorResponse("FilePath is required");
            }

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return CreateErrorResponse("Document not found");
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();

            if (semanticModel == null || syntaxRoot == null)
            {
                return CreateErrorResponse("Could not get semantic model");
            }

            if (request.Line.HasValue && request.Column.HasValue)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
                var node = syntaxRoot.FindToken(position).Parent;

                ISymbol? symbol = null;
                if (node != null)
                {
                    symbol = semanticModel.GetSymbolInfo(node).Symbol;
                    if (symbol == null)
                    {
                        // Fallback for declarations (class, method, property, field, local, parameter)
                        var declNode = node;
                        while (declNode != null && symbol == null)
                        {
                            symbol = semanticModel.GetDeclaredSymbol(declNode);
                            declNode = declNode.Parent;
                        }
                    }
                }

                if (symbol != null)
                {
                    var symbolInfo = await CreateSymbolInfoAsync(symbol);
                    return CreateSuccessResponse(symbolInfo);
                }
            }

            return CreateErrorResponse("Symbol not found");
        }

        public async Task<QueryResponse> FindSymbolAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("SymbolName is required");
            }

            var symbols = new List<RoslynBridge.Models.SymbolInfo>();
            string? kind = null;
            request.Parameters?.TryGetValue("kind", out kind);

            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var matchingSymbols = compilation.GetSymbolsWithName(
                    name => name.IndexOf(request.SymbolName, StringComparison.OrdinalIgnoreCase) >= 0,
                    SymbolFilter.All
                );

                foreach (var symbol in matchingSymbols)
                {
                    // Filter by kind if specified
                    if (!string.IsNullOrEmpty(kind) &&
                        !symbol.Kind.ToString().Equals(kind, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    symbols.Add(await CreateSymbolInfoAsync(symbol));
                }
            }

            return CreateSuccessResponse(symbols);
        }

        public async Task<QueryResponse> GetTypeMembersAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("SymbolName (type name) is required");
            }

            string? includeInheritedStr = null;
            request.Parameters?.TryGetValue("includeInherited", out includeInheritedStr);
            var includeInherited = includeInheritedStr == "true";

            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var typeSymbol = compilation.GetTypeByMetadataName(request.SymbolName) ??
                                 compilation.GetSymbolsWithName(request.SymbolName, SymbolFilter.Type).FirstOrDefault() as INamedTypeSymbol;

                if (typeSymbol != null)
                {
                    var members = includeInherited
                        ? typeSymbol.GetMembers()
                        : typeSymbol.GetMembers().Where(m => m.ContainingType.Equals(typeSymbol, SymbolEqualityComparer.Default));

                    var memberInfos = members.Select(m => new MemberInfo
                    {
                        Name = m.Name,
                        Kind = m.Kind.ToString(),
                        ReturnType = (m as IMethodSymbol)?.ReturnType.ToDisplayString() ??
                                    (m as IPropertySymbol)?.Type.ToDisplayString() ??
                                    (m as IFieldSymbol)?.Type.ToDisplayString(),
                        Signature = m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        Documentation = m.GetDocumentationCommentXml(),
                        Modifiers = GetModifiers(m),
                        Accessibility = m.DeclaredAccessibility.ToString(),
                        IsStatic = m.IsStatic,
                        IsAbstract = m.IsAbstract,
                        IsVirtual = m.IsVirtual,
                        IsOverride = m.IsOverride
                    }).ToList();

                    return CreateSuccessResponse(memberInfos);
                }
            }

            return CreateErrorResponse($"Type '{request.SymbolName}' not found");
        }

        public async Task<QueryResponse> GetTypeHierarchyAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("SymbolName (type name) is required");
            }

            string? direction = null;
            request.Parameters?.TryGetValue("direction", out direction);
            direction = direction ?? "both";

            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var typeSymbol = compilation.GetTypeByMetadataName(request.SymbolName) ??
                                 compilation.GetSymbolsWithName(request.SymbolName, SymbolFilter.Type).FirstOrDefault() as INamedTypeSymbol;

                if (typeSymbol != null)
                {
                    var hierarchy = new TypeHierarchyInfo
                    {
                        TypeName = typeSymbol.Name,
                        FullName = typeSymbol.ToDisplayString(),
                        BaseTypes = new List<string>(),
                        Interfaces = typeSymbol.Interfaces.Select(i => i.ToDisplayString()).ToList(),
                        DerivedTypes = new List<string>()
                    };

                    // Get base types
                    if (direction == "up" || direction == "both")
                    {
                        var baseType = typeSymbol.BaseType;
                        while (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                        {
                            hierarchy.BaseTypes.Add(baseType.ToDisplayString());
                            baseType = baseType.BaseType;
                        }
                    }

                    // Get derived types
                    if (direction == "down" || direction == "both")
                    {
                        var derivedTypes = await SymbolFinder.FindDerivedClassesAsync(typeSymbol, Workspace.CurrentSolution, true);
                        hierarchy.DerivedTypes = derivedTypes.Select(t => t.ToDisplayString()).ToList();
                    }

                    return CreateSuccessResponse(hierarchy);
                }
            }

            return CreateErrorResponse($"Type '{request.SymbolName}' not found");
        }

        public async Task<QueryResponse> FindImplementationsAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName) && string.IsNullOrEmpty(request.FilePath))
            {
                return CreateErrorResponse("Either SymbolName or FilePath with Line/Column is required");
            }

            ISymbol? targetSymbol = null;

            // Find by name
            if (!string.IsNullOrEmpty(request.SymbolName))
            {
                foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation == null) continue;

                    targetSymbol = compilation.GetTypeByMetadataName(request.SymbolName) ??
                                   compilation.GetSymbolsWithName(request.SymbolName, SymbolFilter.Type).FirstOrDefault();
                    if (targetSymbol != null) break;
                }
            }
            // Find by location
            else if (!string.IsNullOrEmpty(request.FilePath) && request.Line.HasValue && request.Column.HasValue)
            {
                var document = FindDocument(request.FilePath);

                if (document != null)
                {
                    var semanticModel = await document.GetSemanticModelAsync();
                    var syntaxRoot = await document.GetSyntaxRootAsync();
                    var sourceText = await document.GetTextAsync();
                    var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
                    var node = syntaxRoot?.FindToken(position).Parent;
                    targetSymbol = semanticModel?.GetSymbolInfo(node).Symbol;
                }
            }

            if (targetSymbol == null)
            {
                return CreateErrorResponse("Symbol not found");
            }

            var implementations = new List<RoslynBridge.Models.SymbolInfo>();

            if (targetSymbol is INamedTypeSymbol namedType && (namedType.TypeKind == TypeKind.Interface || namedType.IsAbstract))
            {
                var implementers = await SymbolFinder.FindImplementationsAsync(namedType, Workspace.CurrentSolution);
                foreach (var impl in implementers)
                {
                    implementations.Add(await CreateSymbolInfoAsync(impl));
                }
            }

            return CreateSuccessResponse(implementations);
        }

        public async Task<QueryResponse> GetCallHierarchyAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath) || !request.Line.HasValue || !request.Column.HasValue)
            {
                return CreateErrorResponse("FilePath, Line, and Column are required");
            }

            string? direction = null;
            request.Parameters?.TryGetValue("direction", out direction);
            direction = direction ?? "callers";

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return CreateErrorResponse("Document not found");
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
            var node = syntaxRoot?.FindToken(position).Parent;
            var symbol = semanticModel?.GetSymbolInfo(node).Symbol;

            if (symbol == null)
            {
                return CreateErrorResponse("Symbol not found");
            }

            var calls = new List<CallInfo>();

            if (direction == "callers")
            {
                var callers = await SymbolFinder.FindCallersAsync(symbol, Workspace.CurrentSolution);
                foreach (var caller in callers)
                {
                    foreach (var location in caller.Locations)
                    {
                        calls.Add(new CallInfo
                        {
                            CallerName = caller.CallingSymbol.Name,
                            CallerType = caller.CallingSymbol.ContainingType?.Name,
                            Location = new LocationInfo
                            {
                                FilePath = location.SourceTree?.FilePath,
                                StartLine = location.GetLineSpan().StartLinePosition.Line + 1,
                                StartColumn = location.GetLineSpan().StartLinePosition.Character,
                                EndLine = location.GetLineSpan().EndLinePosition.Line + 1,
                                EndColumn = location.GetLineSpan().EndLinePosition.Character
                            }
                        });
                    }
                }
            }

            var result = new CallHierarchyInfo
            {
                SymbolName = symbol.ToDisplayString(),
                Calls = calls
            };

            return CreateSuccessResponse(result);
        }

        public async Task<QueryResponse> GetSymbolContextAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.FilePath) || !request.Line.HasValue || !request.Column.HasValue)
            {
                return CreateErrorResponse("FilePath, Line, and Column are required");
            }

            var document = FindDocument(request.FilePath);

            if (document == null)
            {
                return CreateErrorResponse("Document not found");
            }

            var semanticModel = await document.GetSemanticModelAsync();
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var sourceText = await document.GetTextAsync();
            var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
            var node = syntaxRoot?.FindToken(position).Parent;

            if (node == null || semanticModel == null)
            {
                return CreateErrorResponse("Could not analyze position");
            }

            var symbol = semanticModel.GetSymbolInfo(node).Symbol;
            var containingMethod = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var containingClass = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            var context = new SymbolContextInfo
            {
                ContainingClass = containingClass?.Identifier.Text,
                ContainingMethod = containingMethod?.Identifier.Text,
                ContainingNamespace = semanticModel.GetDeclaredSymbol(containingClass)?.ContainingNamespace?.ToDisplayString(),
                SymbolAtPosition = symbol?.ToDisplayString(),
                LocalVariables = new List<string>(),
                Parameters = new List<string>()
            };

            // Get local variables
            if (containingMethod != null)
            {
                var dataFlow = semanticModel.AnalyzeDataFlow(containingMethod);
                if (dataFlow.Succeeded)
                {
                    context.LocalVariables = dataFlow.VariablesDeclared.Select(v => v.Name).ToList();
                }

                // Get parameters
                var methodSymbol = semanticModel.GetDeclaredSymbol(containingMethod) as IMethodSymbol;
                if (methodSymbol != null)
                {
                    context.Parameters = methodSymbol.Parameters.Select(p => $"{p.Type.Name} {p.Name}").ToList();
                }
            }

            return CreateSuccessResponse(context);
        }
    }
}
