#nullable enable
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

        /// <summary>
        /// Resolve a named type across all projects with fuzzy fallback.
        /// Tries: exact metadata name → exact GetSymbolsWithName → short-name search with FQN suffix match.
        /// </summary>
        private async Task<INamedTypeSymbol?> ResolveTypeAsync(string typeName)
        {
            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                // 1. Exact metadata name lookup
                var typeSymbol = compilation.GetTypeByMetadataName(typeName);
                if (typeSymbol != null) return typeSymbol;

                // 2. Exact simple-name match
                typeSymbol = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type)
                    .FirstOrDefault() as INamedTypeSymbol;
                if (typeSymbol != null) return typeSymbol;
            }

            // 3. Fuzzy fallback: extract short name and search, then verify FQN suffix
            var shortName = typeName.Contains(".") ? typeName.Substring(typeName.LastIndexOf('.') + 1) : typeName;

            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                var candidates = compilation.GetSymbolsWithName(
                    name => name.Equals(shortName, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.Type);

                foreach (var candidate in candidates)
                {
                    if (candidate is INamedTypeSymbol namedType)
                    {
                        var fullName = namedType.ToDisplayString();
                        // Accept if FQN ends with the requested name or short name matches exactly
                        if (fullName.EndsWith(typeName, StringComparison.OrdinalIgnoreCase) ||
                            fullName.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                            namedType.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase))
                        {
                            return namedType;
                        }
                    }
                }
            }

            return null;
        }

        public async Task<QueryResponse> GetSymbolInfoAsync(QueryRequest request)
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

            string? projectFilter = null;
            request.Parameters?.TryGetValue("projectName", out projectFilter);

            string? excludeGeneratedStr = null;
            request.Parameters?.TryGetValue("excludeGenerated", out excludeGeneratedStr);
            var excludeGenerated = excludeGeneratedStr != "false"; // default true

            var projects = Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>();
            if (!string.IsNullOrEmpty(projectFilter))
            {
                projects = projects.Where(p => p.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var project in projects)
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

                    // Exclude symbols declared in generated/designer files
                    if (excludeGenerated && IsInGeneratedFile(symbol))
                    {
                        continue;
                    }

                    symbols.Add(await CreateSymbolInfoAsync(symbol));
                }
            }

            return CreateSuccessResponse(symbols);
        }

        private static bool IsInGeneratedFile(ISymbol symbol)
        {
            foreach (var location in symbol.Locations)
            {
                var filePath = location.SourceTree?.FilePath;
                if (string.IsNullOrEmpty(filePath)) continue;

                var p = filePath.Replace('/', '\\').ToLowerInvariant();
                if (p.EndsWith(".designer.cs") || p.EndsWith(".generated.cs") ||
                    p.EndsWith(".g.cs") || p.EndsWith(".g.i.cs") ||
                    p.Contains("\\obj\\") || p.Contains("\\bin\\"))
                {
                    return true;
                }
            }
            return false;
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

            string? kindFilter = null;
            request.Parameters?.TryGetValue("kind", out kindFilter);

            string? accessibilityFilter = null;
            request.Parameters?.TryGetValue("accessibility", out accessibilityFilter);

            var typeSymbol = await ResolveTypeAsync(request.SymbolName!);

            if (typeSymbol == null)
            {
                return CreateErrorResponse($"Type '{request.SymbolName}' not found");
            }

            var members = includeInherited
                ? typeSymbol.GetMembers()
                : typeSymbol.GetMembers().Where(m => m.ContainingType.Equals(typeSymbol, SymbolEqualityComparer.Default));

            // Filter by kind (Method, Property, Field, Event)
            if (!string.IsNullOrEmpty(kindFilter))
            {
                var kinds = kindFilter.Split(',').Select(k => k.Trim().ToLowerInvariant()).ToHashSet();
                members = members.Where(m => kinds.Contains(m.Kind.ToString().ToLowerInvariant()));
            }

            // Filter by accessibility (Public, Private, Protected, Internal)
            if (!string.IsNullOrEmpty(accessibilityFilter))
            {
                var accessLevels = accessibilityFilter.Split(',').Select(a => a.Trim().ToLowerInvariant()).ToHashSet();
                members = members.Where(m => accessLevels.Contains(m.DeclaredAccessibility.ToString().ToLowerInvariant()));
            }

            // Skip compiler-generated backing fields (e.g., <PropertyName>k__BackingField)
            members = members.Where(m => !m.IsImplicitlyDeclared);

            var memberInfos = members.Select(m =>
            {
                var info = new MemberInfo
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
                };
                if (m is IFieldSymbol fieldSymbol && fieldSymbol.HasConstantValue)
                {
                    info.Value = fieldSymbol.ConstantValue?.ToString();
                }
                return info;
            }).ToList();

            return CreateSuccessResponse(memberInfos);
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

            var typeSymbol = await ResolveTypeAsync(request.SymbolName!);

            if (typeSymbol == null)
            {
                return CreateErrorResponse($"Type '{request.SymbolName}' not found");
            }

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
                if (Workspace?.CurrentSolution == null)
                {
                    return CreateErrorResponse("Workspace not available");
                }
                var derivedTypes = await SymbolFinder.FindDerivedClassesAsync(typeSymbol, Workspace.CurrentSolution, true);
                hierarchy.DerivedTypes = derivedTypes.Select(t => t.ToDisplayString()).ToList();
            }

            return CreateSuccessResponse(hierarchy);
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
                targetSymbol = await ResolveTypeAsync(request.SymbolName!);
            }
            // Find by location
            else if (!string.IsNullOrEmpty(request.FilePath) && request.Line.HasValue && request.Column.HasValue)
            {
                var document = FindDocument(request.FilePath!);

                if (document != null)
                {
                    var semanticModel = await document.GetSemanticModelAsync();
                    var syntaxRoot = await document.GetSyntaxRootAsync();
                    var sourceText = await document.GetTextAsync();
                    var position = sourceText.Lines[request.Line.Value - 1].Start + request.Column.Value;
                    var node = syntaxRoot?.FindToken(position).Parent;

                    if (node != null && semanticModel != null)
                    {
                        targetSymbol = semanticModel.GetSymbolInfo(node).Symbol;
                        if (targetSymbol == null)
                        {
                            var declNode = node;
                            while (declNode != null && targetSymbol == null)
                            {
                                targetSymbol = semanticModel.GetDeclaredSymbol(declNode);
                                declNode = declNode.Parent;
                            }
                        }
                    }
                }
            }

            if (targetSymbol == null)
            {
                return CreateErrorResponse("Symbol not found");
            }

            var implementations = new List<RoslynBridge.Models.SymbolInfo>();

            if (targetSymbol is INamedTypeSymbol namedType && (namedType.TypeKind == TypeKind.Interface || namedType.IsAbstract))
            {
                if (Workspace?.CurrentSolution == null)
                {
                    return CreateErrorResponse("Workspace not available");
                }
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

            var document = FindDocument(request.FilePath!);

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
                return CreateErrorResponse("Syntax node not found at specified position");
            }

            var symbol = semanticModel.GetSymbolInfo(node).Symbol;
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
                return CreateErrorResponse("Symbol not found");
            }

            if (Workspace?.CurrentSolution == null)
            {
                return CreateErrorResponse("Workspace not available");
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

            var document = FindDocument(request.FilePath!);

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
            if (symbol == null)
            {
                var declNode = node;
                while (declNode != null && symbol == null)
                {
                    symbol = semanticModel.GetDeclaredSymbol(declNode);
                    declNode = declNode.Parent;
                }
            }
            var containingMethod = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            var containingClass = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            var context = new SymbolContextInfo
            {
                ContainingClass = containingClass?.Identifier.Text,
                ContainingMethod = containingMethod?.Identifier.Text,
                ContainingNamespace = containingClass != null ? semanticModel.GetDeclaredSymbol(containingClass)?.ContainingNamespace?.ToDisplayString() : null,
                SymbolAtPosition = symbol?.ToDisplayString(),
                LocalVariables = new List<string>(),
                Parameters = new List<string>()
            };

            // Get local variables and parameters
            if (containingMethod != null)
            {
                DataFlowAnalysis? dataFlow = null;
                if (containingMethod.Body != null)
                {
                    dataFlow = semanticModel.AnalyzeDataFlow(containingMethod.Body);
                }
                else if (containingMethod.ExpressionBody != null)
                {
                    dataFlow = semanticModel.AnalyzeDataFlow(containingMethod.ExpressionBody.Expression);
                }

                if (dataFlow != null && dataFlow.Succeeded)
                {
                    context.LocalVariables = dataFlow.VariablesDeclared.Select(v => v.Name).ToList();
                }

                var methodSymbol = semanticModel.GetDeclaredSymbol(containingMethod) as IMethodSymbol;
                if (methodSymbol != null)
                {
                    context.Parameters = methodSymbol.Parameters.Select(p => $"{p.Type.Name} {p.Name}").ToList();
                }
            }

            return CreateSuccessResponse(context);
        }

        public async Task<QueryResponse> GetSymbolSourceAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("SymbolName is required");
            }

            var results = new List<SymbolSourceInfo>();
            var searchName = request.SymbolName!;

            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                // Try exact match first, then ends-with fallback
                var symbols = compilation.GetSymbolsWithName(
                    name => name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.All);

                if (!symbols.Any())
                {
                    symbols = compilation.GetSymbolsWithName(
                        name => searchName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase) ||
                                name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
                        SymbolFilter.All);
                }

                foreach (var symbol in symbols)
                {
                    // Check if fully qualified name matches for dotted names
                    if (searchName.Contains("."))
                    {
                        var fullName = symbol.ToDisplayString();
                        if (!fullName.EndsWith(searchName, StringComparison.OrdinalIgnoreCase) &&
                            !fullName.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
                    {
                        var node = await syntaxRef.GetSyntaxAsync();
                        var lineSpan = node.GetLocation().GetLineSpan();

                        // Get source: node.ToString() excludes leading/trailing trivia from adjacent declarations
                        var source = node.ToString();

                        // Prepend XML doc comment trivia if present
                        var leadingTrivia = node.GetLeadingTrivia();
                        var docComment = leadingTrivia
                            .Where(t => t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                                        t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineDocumentationCommentTrivia))
                            .Select(t => t.ToFullString())
                            .FirstOrDefault();

                        if (docComment != null)
                        {
                            source = docComment + source;
                        }

                        results.Add(new SymbolSourceInfo
                        {
                            SymbolName = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            Kind = symbol.Kind.ToString(),
                            FilePath = lineSpan.Path,
                            StartLine = lineSpan.StartLinePosition.Line + 1,
                            EndLine = lineSpan.EndLinePosition.Line + 1,
                            Source = source
                        });
                    }
                }
            }

            if (!results.Any())
            {
                return CreateErrorResponse($"Symbol '{request.SymbolName}' not found");
            }

            // Deduplicate by file+line (same symbol found via multiple projects)
            results = results
                .GroupBy(r => $"{r.FilePath}:{r.StartLine}")
                .Select(g => g.First())
                .ToList();

            return CreateSuccessResponse(results);
        }

        public async Task<QueryResponse> FindUsagesAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("SymbolName is required");
            }

            if (Workspace?.CurrentSolution == null)
            {
                return CreateErrorResponse("Workspace not available");
            }

            var searchName = request.SymbolName!;
            var allSymbols = new List<ISymbol>();

            foreach (var project in Workspace.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                // Exact match first, then ends-with fallback
                var symbols = compilation.GetSymbolsWithName(
                    name => name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.All);

                if (!symbols.Any())
                {
                    symbols = compilation.GetSymbolsWithName(
                        name => searchName.EndsWith("." + name, StringComparison.OrdinalIgnoreCase) ||
                                name.Equals(searchName, StringComparison.OrdinalIgnoreCase),
                        SymbolFilter.All);
                }

                foreach (var symbol in symbols)
                {
                    if (searchName.Contains("."))
                    {
                        var fullName = symbol.ToDisplayString();
                        if (!fullName.EndsWith(searchName, StringComparison.OrdinalIgnoreCase) &&
                            !fullName.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                    allSymbols.Add(symbol);
                }
            }

            if (!allSymbols.Any())
            {
                return CreateErrorResponse($"Symbol '{request.SymbolName}' not found");
            }

            // Deduplicate symbols (same symbol found via multiple project compilations)
            var uniqueSymbols = allSymbols
                .GroupBy(s => s.ToDisplayString())
                .Select(g => g.First())
                .ToList();

            var filesByProject = new Dictionary<string, HashSet<string>>();
            var totalReferences = 0;

            foreach (var symbol in uniqueSymbols)
            {
                var references = await SymbolFinder.FindReferencesAsync(symbol, Workspace.CurrentSolution);
                foreach (var refSymbol in references)
                {
                    foreach (var location in refSymbol.Locations)
                    {
                        totalReferences++;
                        var doc = location.Document;
                        var projectName = doc.Project.Name;
                        var filePath = doc.FilePath ?? doc.Name;

                        if (!filesByProject.ContainsKey(projectName))
                            filesByProject[projectName] = new HashSet<string>();
                        filesByProject[projectName].Add(filePath);
                    }
                }
            }

            var result = new UsageInfo
            {
                SymbolName = uniqueSymbols.First().ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                TotalReferences = totalReferences,
                FileCount = filesByProject.Values.Sum(s => s.Count),
                FilesByProject = filesByProject.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.OrderBy(f => f).ToList())
            };

            return CreateSuccessResponse(result);
        }

        public async Task<QueryResponse> FindCallersAsync(QueryRequest request)
        {
            if (string.IsNullOrEmpty(request.SymbolName))
            {
                return CreateErrorResponse("SymbolName is required (e.g., 'MyClass.MyMethod' or 'MyMethod')");
            }

            if (Workspace?.CurrentSolution == null)
            {
                return CreateErrorResponse("Workspace not available");
            }

            var searchName = request.SymbolName!;
            var allSymbols = new List<ISymbol>();

            foreach (var project in Workspace.CurrentSolution.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                if (compilation == null) continue;

                // Extract the method/member name (last segment after dot)
                var memberName = searchName.Contains(".") ? searchName.Substring(searchName.LastIndexOf('.') + 1) : searchName;

                var symbols = compilation.GetSymbolsWithName(
                    name => name.Equals(memberName, StringComparison.OrdinalIgnoreCase),
                    SymbolFilter.All);

                foreach (var symbol in symbols)
                {
                    // For dotted names, verify the containing type matches
                    if (searchName.Contains("."))
                    {
                        var fullName = symbol.ToDisplayString();
                        if (!fullName.EndsWith(searchName, StringComparison.OrdinalIgnoreCase) &&
                            !fullName.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Also try ContainingType.Name + "." + Name
                            var qualName = symbol.ContainingType != null
                                ? symbol.ContainingType.Name + "." + symbol.Name
                                : symbol.Name;
                            if (!qualName.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }
                    }
                    allSymbols.Add(symbol);
                }
            }

            if (!allSymbols.Any())
            {
                return CreateErrorResponse($"Symbol '{request.SymbolName}' not found");
            }

            // Deduplicate
            var uniqueSymbols = allSymbols
                .GroupBy(s => s.ToDisplayString())
                .Select(g => g.First())
                .ToList();

            var callers = new List<CallerInfo>();

            foreach (var symbol in uniqueSymbols)
            {
                var callerResults = await SymbolFinder.FindCallersAsync(symbol, Workspace.CurrentSolution);
                foreach (var caller in callerResults)
                {
                    foreach (var location in caller.Locations)
                    {
                        callers.Add(new CallerInfo
                        {
                            CallerName = caller.CallingSymbol.Name,
                            CallerType = caller.CallingSymbol.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            CallerSignature = caller.CallingSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            FilePath = location.SourceTree?.FilePath,
                            Line = location.GetLineSpan().StartLinePosition.Line + 1,
                            ProjectName = caller.CallingSymbol.ContainingAssembly?.Name
                        });
                    }
                }
            }

            return CreateSuccessResponse(new
            {
                symbolName = uniqueSymbols.First().ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                totalCallers = callers.Count,
                callers = callers
            });
        }
    }
}
