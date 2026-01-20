#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;
using RoslynBridge.Services.Analysis;

namespace RoslynBridge.Services
{
    /// <summary>
    /// Service for detecting code smells using Roslyn analysis
    /// </summary>
    public class CodeSmellAnalysisService : BaseRoslynService
    {
        private readonly DuplicateCodeAnalyzer _duplicateAnalyzer = new();

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
                        if (SyntaxMetrics.IsGeneratedFile(document.FilePath))
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

        /// <summary>
        /// Detect duplicate code blocks across the solution
        /// </summary>
        public async Task<QueryResponse> GetDuplicatesAsync(QueryRequest request)
        {
            var minLines = 5; // Default minimum lines
            var minSimilarity = 80; // Default 80% similarity
            string? classNameFilter = null;
            string? namespaceFilter = null;

            if (request.Parameters != null)
            {
                if (request.Parameters.TryGetValue("minLines", out var minLinesStr) && int.TryParse(minLinesStr, out var ml))
                    minLines = ml;
                if (request.Parameters.TryGetValue("similarity", out var simStr) && int.TryParse(simStr, out var sim))
                    minSimilarity = sim;
                if (request.Parameters.TryGetValue("className", out var cn))
                    classNameFilter = cn;
                if (request.Parameters.TryGetValue("namespace", out var ns))
                    namespaceFilter = ns;
            }

            var projects = Workspace?.CurrentSolution.Projects ?? Enumerable.Empty<Project>();
            var duplicates = await _duplicateAnalyzer.FindDuplicatesAsync(
                projects, minLines, minSimilarity, classNameFilter, namespaceFilter);

            return CreateSuccessResponse(duplicates, $"Found {duplicates.Count} duplicate(s)");
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
            var methodSymbol = semanticModel.GetDeclaredSymbol(method) as IMethodSymbol;

            if (methodSymbol == null)
                return smells;

            var location = SyntaxMetrics.CreateLocationInfo(method.GetLocation());

            // Check method length
            var lineCount = SyntaxMetrics.GetMethodLineCount(method);
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
            var complexity = ComplexityCalculator.Calculate(method);
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
            var nestingDepth = NestingDepthCalculator.Calculate(method);
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
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;

            if (classSymbol == null)
                return smells;

            var location = SyntaxMetrics.CreateLocationInfo(classDecl.GetLocation());

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
            var classLineCount = SyntaxMetrics.GetClassLineCount(classDecl);
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
        private static (string severity, int priorityScore) CalculateSeverityAndPriority(int actualValue, int threshold, double weight = 1.0)
        {
            var ratio = (double)actualValue / threshold;

            // Calculate base score (0-100)
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

        private static CodeSmellThresholds ParseThresholds(Dictionary<string, string>? parameters)
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
    }
}
