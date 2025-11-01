#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    /// <summary>
    /// Service for detecting code smells using Roslyn analysis
    /// </summary>
    public class CodeSmellAnalysisService : BaseRoslynService
    {
        public CodeSmellAnalysisService(AsyncPackage package, IWorkspaceProvider workspaceProvider)
            : base(package, workspaceProvider)
        {
        }

        /// <summary>
        /// Analyze code for various code smells
        /// </summary>
        public async Task<QueryResponse> GetCodeSmellsAsync(QueryRequest request)
        {
            var thresholds = ParseThresholds(request.Parameters);
            var smells = new List<CodeSmellInfo>();

            if (!string.IsNullOrEmpty(request.FilePath))
            {
                // Analyze specific file
                var document = FindDocument(request.FilePath!);
                if (document == null)
                {
                    return CreateErrorResponse("Document not found");
                }

                var fileSmells = await AnalyzeDocumentAsync(document, thresholds);
                smells.AddRange(fileSmells);
            }
            else
            {
                // Analyze entire solution
                var projectFilter = request.Parameters?.ContainsKey("projectName") == true
                    ? request.Parameters["projectName"]
                    : null;

                foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
                {
                    // Filter by project if specified
                    if (!string.IsNullOrEmpty(projectFilter) &&
                        !project.Name.Equals(projectFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var document in project.Documents)
                    {
                        if (IsGeneratedFile(document.FilePath))
                            continue;

                        var fileSmells = await AnalyzeDocumentAsync(document, thresholds, project.Name);
                        smells.AddRange(fileSmells);
                    }
                }
            }

            // Filter by smell type if specified
            if (request.Parameters != null && request.Parameters.TryGetValue("smellType", out var smellType))
            {
                smells = smells.Where(s => s.SmellType?.Equals(smellType, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            // Filter by severity if specified
            if (request.Parameters != null && request.Parameters.TryGetValue("severity", out var severity))
            {
                smells = smells.Where(s => s.Severity?.Equals(severity, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }

            // Sort by priority score (highest first)
            smells = smells.OrderByDescending(s => s.PriorityScore).ToList();

            // Limit to top N if specified
            if (request.Parameters != null && request.Parameters.TryGetValue("top", out var topStr) && int.TryParse(topStr, out var top))
            {
                smells = smells.Take(top).ToList();
            }

            return CreateSuccessResponse(smells, $"Found {smells.Count} code smell(s)");
        }

        /// <summary>
        /// Get summary of code smells across the solution
        /// </summary>
        public async Task<QueryResponse> GetCodeSmellSummaryAsync(QueryRequest request)
        {
            var allSmells = await GetCodeSmellsAsync(request);
            if (!allSmells.Success || allSmells.Data == null)
            {
                return allSmells;
            }

            var smellsList = (allSmells.Data as List<CodeSmellInfo>) ?? new List<CodeSmellInfo>();
            var thresholds = ParseThresholds(request.Parameters);

            var summary = new CodeSmellSummary
            {
                TotalSmells = smellsList.Count,
                LongMethods = smellsList.Count(s => s.SmellType == "LongMethod"),
                HighComplexity = smellsList.Count(s => s.SmellType == "HighComplexity"),
                TooManyParameters = smellsList.Count(s => s.SmellType == "TooManyParameters"),
                DeepNesting = smellsList.Count(s => s.SmellType == "DeepNesting"),
                LargeClasses = smellsList.Count(s => s.SmellType == "LargeClass"),
                LongClasses = smellsList.Count(s => s.SmellType == "LongClass"),
                Thresholds = thresholds
            };

            return CreateSuccessResponse(summary);
        }

        private async Task<List<CodeSmellInfo>> AnalyzeDocumentAsync(Document document, CodeSmellThresholds thresholds, string? projectName = null)
        {
            var smells = new List<CodeSmellInfo>();

            var syntaxRoot = await document.GetSyntaxRootAsync();
            var semanticModel = await document.GetSemanticModelAsync();

            if (syntaxRoot == null || semanticModel == null)
                return smells;

            // Analyze methods
            var methodDeclarations = syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methodDeclarations)
            {
                smells.AddRange(AnalyzeMethod(method, semanticModel, thresholds, projectName));
            }

            // Analyze classes
            var classDeclarations = syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classDeclarations)
            {
                smells.AddRange(AnalyzeClass(classDecl, semanticModel, thresholds, projectName));
            }

            return smells;
        }

        private List<CodeSmellInfo> AnalyzeMethod(MethodDeclarationSyntax method, SemanticModel semanticModel, CodeSmellThresholds thresholds, string? projectName = null)
        {
            var smells = new List<CodeSmellInfo>();
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            if (methodSymbol == null)
                return smells;

            var location = CreateLocationInfo(method.GetLocation());

            // Check method length
            var lineCount = GetMethodLineCount(method);
            if (lineCount > thresholds.MethodLength)
            {
                var (severity, priorityScore) = CalculateSeverityAndPriority(lineCount, thresholds.MethodLength, 1.5);
                smells.Add(new CodeSmellInfo
                {
                    SmellType = "LongMethod",
                    Severity = severity,
                    PriorityScore = priorityScore,
                    SymbolName = methodSymbol.ToDisplayString(),
                    Name = methodSymbol.Name,
                    SymbolKind = "Method",
                    ProjectName = projectName,
                    Location = location,
                    ActualValue = lineCount,
                    ThresholdValue = thresholds.MethodLength,
                    Message = $"Method has {lineCount} lines (threshold: {thresholds.MethodLength})"
                });
            }

            // Check cyclomatic complexity
            var complexity = CalculateComplexity(method);
            if (complexity > thresholds.CyclomaticComplexity)
            {
                var (severity, priorityScore) = CalculateSeverityAndPriority(complexity, thresholds.CyclomaticComplexity, 2.0);
                smells.Add(new CodeSmellInfo
                {
                    SmellType = "HighComplexity",
                    Severity = severity,
                    PriorityScore = priorityScore,
                    SymbolName = methodSymbol.ToDisplayString(),
                    Name = methodSymbol.Name,
                    SymbolKind = "Method",
                    ProjectName = projectName,
                    Location = location,
                    ActualValue = complexity,
                    ThresholdValue = thresholds.CyclomaticComplexity,
                    Message = $"Complexity score: {complexity} (threshold: {thresholds.CyclomaticComplexity})"
                });
            }

            // Check parameter count
            var paramCount = methodSymbol.Parameters.Length;
            if (paramCount > thresholds.ParameterCount)
            {
                var (severity, priorityScore) = CalculateSeverityAndPriority(paramCount, thresholds.ParameterCount, 1.2);
                smells.Add(new CodeSmellInfo
                {
                    SmellType = "TooManyParameters",
                    Severity = severity,
                    PriorityScore = priorityScore,
                    SymbolName = methodSymbol.ToDisplayString(),
                    Name = methodSymbol.Name,
                    SymbolKind = "Method",
                    ProjectName = projectName,
                    Location = location,
                    ActualValue = paramCount,
                    ThresholdValue = thresholds.ParameterCount,
                    Message = $"Method has {paramCount} parameters (threshold: {thresholds.ParameterCount})"
                });
            }

            // Check nesting depth
            var nestingDepth = CalculateNestingDepth(method);
            if (nestingDepth > thresholds.NestingDepth)
            {
                var (severity, priorityScore) = CalculateSeverityAndPriority(nestingDepth, thresholds.NestingDepth, 1.3);
                smells.Add(new CodeSmellInfo
                {
                    SmellType = "DeepNesting",
                    Severity = severity,
                    PriorityScore = priorityScore,
                    SymbolName = methodSymbol.ToDisplayString(),
                    Name = methodSymbol.Name,
                    SymbolKind = "Method",
                    ProjectName = projectName,
                    Location = location,
                    ActualValue = nestingDepth,
                    ThresholdValue = thresholds.NestingDepth,
                    Message = $"Maximum nesting depth: {nestingDepth} (threshold: {thresholds.NestingDepth})"
                });
            }

            return smells;
        }

        private List<CodeSmellInfo> AnalyzeClass(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CodeSmellThresholds thresholds, string? projectName = null)
        {
            var smells = new List<CodeSmellInfo>();
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);

            if (classSymbol == null)
                return smells;

            var location = CreateLocationInfo(classDecl.GetLocation());

            // Check member count
            var memberCount = classSymbol.GetMembers().Count(m =>
                m.Kind == SymbolKind.Method ||
                m.Kind == SymbolKind.Property ||
                m.Kind == SymbolKind.Field ||
                m.Kind == SymbolKind.Event);

            if (memberCount > thresholds.ClassMembers)
            {
                var (severity, priorityScore) = CalculateSeverityAndPriority(memberCount, thresholds.ClassMembers, 1.4);
                smells.Add(new CodeSmellInfo
                {
                    SmellType = "LargeClass",
                    Severity = severity,
                    PriorityScore = priorityScore,
                    SymbolName = classSymbol.ToDisplayString(),
                    Name = classSymbol.Name,
                    SymbolKind = "Class",
                    ProjectName = projectName,
                    Location = location,
                    ActualValue = memberCount,
                    ThresholdValue = thresholds.ClassMembers,
                    Message = $"Class has {memberCount} members (threshold: {thresholds.ClassMembers})"
                });
            }

            // Check class length
            var classLineCount = GetClassLineCount(classDecl);
            if (classLineCount > thresholds.ClassLength)
            {
                var (severity, priorityScore) = CalculateSeverityAndPriority(classLineCount, thresholds.ClassLength, 1.3);
                smells.Add(new CodeSmellInfo
                {
                    SmellType = "LongClass",
                    Severity = severity,
                    PriorityScore = priorityScore,
                    SymbolName = classSymbol.ToDisplayString(),
                    Name = classSymbol.Name,
                    SymbolKind = "Class",
                    ProjectName = projectName,
                    Location = location,
                    ActualValue = classLineCount,
                    ThresholdValue = thresholds.ClassLength,
                    Message = $"Class has {classLineCount} lines (threshold: {thresholds.ClassLength})"
                });
            }

            return smells;
        }

        /// <summary>
        /// Calculate severity level and priority score based on how far over threshold
        /// </summary>
        /// <param name="actualValue">Actual measured value</param>
        /// <param name="threshold">Configured threshold</param>
        /// <param name="weight">Weight factor for this metric (higher = more important)</param>
        /// <returns>Tuple of (severity, priorityScore)</returns>
        private (string severity, int priorityScore) CalculateSeverityAndPriority(int actualValue, int threshold, double weight = 1.0)
        {
            var ratio = (double)actualValue / threshold;

            // Calculate base score (0-100)
            // Formula: min(100, (ratio - 1) * 100 * weight)
            var baseScore = Math.Min(100, (ratio - 1.0) * 100 * weight);
            var priorityScore = (int)Math.Round(baseScore);

            // Determine severity level
            string severity;
            if (ratio >= 4.0)
                severity = "Critical";  // 4x+ over threshold
            else if (ratio >= 2.5)
                severity = "High";      // 2.5-4x over threshold
            else if (ratio >= 1.5)
                severity = "Medium";    // 1.5-2.5x over threshold
            else
                severity = "Low";       // 1-1.5x over threshold

            return (severity, priorityScore);
        }

        private int GetMethodLineCount(MethodDeclarationSyntax method)
        {
            if (method.Body == null && method.ExpressionBody == null)
                return 0;

            var span = method.Body?.Span ?? method.ExpressionBody!.Span;
            var text = method.SyntaxTree.GetText();
            var startLine = text.Lines.GetLineFromPosition(span.Start).LineNumber;
            var endLine = text.Lines.GetLineFromPosition(span.End).LineNumber;

            return endLine - startLine + 1;
        }

        private int GetClassLineCount(ClassDeclarationSyntax classDecl)
        {
            var span = classDecl.Span;
            var text = classDecl.SyntaxTree.GetText();
            var startLine = text.Lines.GetLineFromPosition(span.Start).LineNumber;
            var endLine = text.Lines.GetLineFromPosition(span.End).LineNumber;

            return endLine - startLine + 1;
        }

        private int CalculateComplexity(MethodDeclarationSyntax method)
        {
            var calculator = new ComplexityCalculator();
            calculator.Visit(method);
            return calculator.Complexity;
        }

        private int CalculateNestingDepth(MethodDeclarationSyntax method)
        {
            var calculator = new NestingDepthCalculator();
            calculator.Visit(method);
            return calculator.MaxDepth;
        }

        private LocationInfo CreateLocationInfo(Location location)
        {
            var lineSpan = location.GetLineSpan();
            return new LocationInfo
            {
                FilePath = lineSpan.Path,
                StartLine = lineSpan.StartLinePosition.Line + 1,
                StartColumn = lineSpan.StartLinePosition.Character,
                EndLine = lineSpan.EndLinePosition.Line + 1,
                EndColumn = lineSpan.EndLinePosition.Character
            };
        }

        private CodeSmellThresholds ParseThresholds(Dictionary<string, string>? parameters)
        {
            var thresholds = new CodeSmellThresholds();

            if (parameters == null)
                return thresholds;

            if (parameters.TryGetValue("methodLength", out var methodLength) && int.TryParse(methodLength, out var ml))
                thresholds.MethodLength = ml;

            if (parameters.TryGetValue("complexity", out var complexity) && int.TryParse(complexity, out var c))
                thresholds.CyclomaticComplexity = c;

            if (parameters.TryGetValue("parameterCount", out var paramCount) && int.TryParse(paramCount, out var pc))
                thresholds.ParameterCount = pc;

            if (parameters.TryGetValue("nestingDepth", out var nestingDepth) && int.TryParse(nestingDepth, out var nd))
                thresholds.NestingDepth = nd;

            if (parameters.TryGetValue("classMembers", out var classMembers) && int.TryParse(classMembers, out var cm))
                thresholds.ClassMembers = cm;

            if (parameters.TryGetValue("classLength", out var classLength) && int.TryParse(classLength, out var cl))
                thresholds.ClassLength = cl;

            return thresholds;
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

        /// <summary>
        /// Calculates cyclomatic complexity of a method
        /// </summary>
        private class ComplexityCalculator : CSharpSyntaxWalker
        {
            public int Complexity { get; private set; } = 1; // Base complexity

            public override void VisitIfStatement(IfStatementSyntax node)
            {
                Complexity++;
                base.VisitIfStatement(node);
            }

            public override void VisitWhileStatement(WhileStatementSyntax node)
            {
                Complexity++;
                base.VisitWhileStatement(node);
            }

            public override void VisitForStatement(ForStatementSyntax node)
            {
                Complexity++;
                base.VisitForStatement(node);
            }

            public override void VisitForEachStatement(ForEachStatementSyntax node)
            {
                Complexity++;
                base.VisitForEachStatement(node);
            }

            public override void VisitSwitchSection(SwitchSectionSyntax node)
            {
                // Each case adds to complexity
                Complexity++;
                base.VisitSwitchSection(node);
            }

            public override void VisitCatchClause(CatchClauseSyntax node)
            {
                Complexity++;
                base.VisitCatchClause(node);
            }

            public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
            {
                Complexity++;
                base.VisitConditionalExpression(node);
            }

            public override void VisitBinaryExpression(BinaryExpressionSyntax node)
            {
                // Logical AND/OR add to complexity
                if (node.Kind() == SyntaxKind.LogicalAndExpression ||
                    node.Kind() == SyntaxKind.LogicalOrExpression ||
                    node.Kind() == SyntaxKind.CoalesceExpression)
                {
                    Complexity++;
                }
                base.VisitBinaryExpression(node);
            }
        }

        /// <summary>
        /// Calculates maximum nesting depth in a method
        /// </summary>
        private class NestingDepthCalculator : CSharpSyntaxWalker
        {
            private int _currentDepth = 0;
            public int MaxDepth { get; private set; } = 0;

            public override void VisitIfStatement(IfStatementSyntax node)
            {
                VisitNested(() => base.VisitIfStatement(node));
            }

            public override void VisitWhileStatement(WhileStatementSyntax node)
            {
                VisitNested(() => base.VisitWhileStatement(node));
            }

            public override void VisitForStatement(ForStatementSyntax node)
            {
                VisitNested(() => base.VisitForStatement(node));
            }

            public override void VisitForEachStatement(ForEachStatementSyntax node)
            {
                VisitNested(() => base.VisitForEachStatement(node));
            }

            public override void VisitSwitchStatement(SwitchStatementSyntax node)
            {
                VisitNested(() => base.VisitSwitchStatement(node));
            }

            public override void VisitTryStatement(TryStatementSyntax node)
            {
                VisitNested(() => base.VisitTryStatement(node));
            }

            private void VisitNested(Action visitAction)
            {
                _currentDepth++;
                if (_currentDepth > MaxDepth)
                    MaxDepth = _currentDepth;

                visitAction();

                _currentDepth--;
            }
        }

        /// <summary>
        /// Detect duplicate code blocks across the solution
        /// </summary>
        public async Task<QueryResponse> GetDuplicatesAsync(QueryRequest request)
        {
            var minLines = 5; // Default minimum lines
            var minSimilarity = 80; // Default 80% similarity

            if (request.Parameters != null)
            {
                if (request.Parameters.TryGetValue("minLines", out var minLinesStr) && int.TryParse(minLinesStr, out var ml))
                    minLines = ml;
                if (request.Parameters.TryGetValue("similarity", out var simStr) && int.TryParse(simStr, out var sim))
                    minSimilarity = sim;
            }

            var duplicates = new List<DuplicateCodeInfo>();
            var methodInfos = new List<(string projectName, IMethodSymbol symbol, MethodDeclarationSyntax syntax, string[] tokens)>();

            // Collect all methods from the solution
            foreach (var project in Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>())
            {
                foreach (var document in project.Documents)
                {
                    if (IsGeneratedFile(document.FilePath))
                        continue;

                    var syntaxRoot = await document.GetSyntaxRootAsync();
                    var semanticModel = await document.GetSemanticModelAsync();

                    if (syntaxRoot == null || semanticModel == null)
                        continue;

                    var methods = syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        var methodSymbol = semanticModel.GetDeclaredSymbol(method);
                        if (methodSymbol == null)
                            continue;

                        var lineCount = GetMethodLineCount(method);
                        if (lineCount < minLines)
                            continue;

                        // Extract tokens for comparison (ignore trivia/whitespace)
                        var tokens = ExtractMethodTokens(method);
                        methodInfos.Add((project.Name, methodSymbol, method, tokens));
                    }
                }
            }

            // Compare all pairs of methods
            for (int i = 0; i < methodInfos.Count; i++)
            {
                for (int j = i + 1; j < methodInfos.Count; j++)
                {
                    var (proj1, sym1, syntax1, tokens1) = methodInfos[i];
                    var (proj2, sym2, syntax2, tokens2) = methodInfos[j];

                    // Skip comparing a method to itself
                    if (sym1.Equals(sym2))
                        continue;

                    var similarity = CalculateTokenSimilarity(tokens1, tokens2);
                    if (similarity >= minSimilarity)
                    {
                        var lineCount = Math.Min(GetMethodLineCount(syntax1), GetMethodLineCount(syntax2));
                        duplicates.Add(new DuplicateCodeInfo
                        {
                            Original = CreateDuplicateLocation(proj1, sym1, syntax1),
                            Duplicate = CreateDuplicateLocation(proj2, sym2, syntax2),
                            SimilarityPercent = similarity,
                            LineCount = lineCount,
                            TokenCount = Math.Min(tokens1.Length, tokens2.Length),
                            Message = $"{similarity}% similar code in {sym1.Name} and {sym2.Name}"
                        });
                    }
                }
            }

            return CreateSuccessResponse(duplicates, $"Found {duplicates.Count} duplicate(s)");
        }

        private string[] ExtractMethodTokens(MethodDeclarationSyntax method)
        {
            var body = method.Body ?? (SyntaxNode?)method.ExpressionBody;
            if (body == null)
                return Array.Empty<string>();

            // Get all tokens, excluding trivia (whitespace, comments)
            return body.DescendantTokens()
                .Where(t => !t.IsKind(SyntaxKind.None))
                .Select(t => t.Kind().ToString())
                .ToArray();
        }

        private int CalculateTokenSimilarity(string[] tokens1, string[] tokens2)
        {
            if (tokens1.Length == 0 || tokens2.Length == 0)
                return 0;

            // Simple token-based similarity using Longest Common Subsequence
            var lcsLength = LongestCommonSubsequence(tokens1, tokens2);
            var maxLength = Math.Max(tokens1.Length, tokens2.Length);

            return (int)((double)lcsLength / maxLength * 100);
        }

        private int LongestCommonSubsequence(string[] seq1, string[] seq2)
        {
            int m = seq1.Length;
            int n = seq2.Length;
            var dp = new int[m + 1, n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (seq1[i - 1] == seq2[j - 1])
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    else
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }

            return dp[m, n];
        }

        private DuplicateLocation CreateDuplicateLocation(string projectName, IMethodSymbol symbol, MethodDeclarationSyntax syntax)
        {
            var location = syntax.GetLocation();
            var lineSpan = location.GetLineSpan();

            return new DuplicateLocation
            {
                ProjectName = projectName,
                FilePath = lineSpan.Path,
                SymbolName = symbol.ToDisplayString(),
                SymbolKind = "Method",
                StartLine = lineSpan.StartLinePosition.Line + 1,
                EndLine = lineSpan.EndLinePosition.Line + 1
            };
        }
    }
}
