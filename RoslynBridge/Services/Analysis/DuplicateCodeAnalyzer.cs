#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynBridge.Models;

namespace RoslynBridge.Services.Analysis
{
    /// <summary>
    /// Analyzes code for duplicate or similar code blocks using token-based comparison.
    /// </summary>
    internal class DuplicateCodeAnalyzer
    {
        /// <summary>
        /// Finds duplicate code blocks across projects in a solution.
        /// </summary>
        public async Task<List<DuplicateCodeInfo>> FindDuplicatesAsync(
            IEnumerable<Project> projects,
            int minLines,
            int minSimilarity,
            string? classNameFilter,
            string? namespaceFilter)
        {
            var duplicates = new List<DuplicateCodeInfo>();
            var methodInfos = new List<MethodInfo>();

            // Collect all methods from the solution
            foreach (var project in projects)
            {
                foreach (var document in project.Documents)
                {
                    if (SyntaxMetrics.IsGeneratedFile(document.FilePath))
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

                        // Apply class name filter
                        if (!string.IsNullOrEmpty(classNameFilter))
                        {
                            var containingType = methodSymbol.ContainingType?.Name;
                            if (containingType == null || containingType.IndexOf(classNameFilter, StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                        }

                        // Apply namespace filter
                        if (!string.IsNullOrEmpty(namespaceFilter))
                        {
                            var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString();
                            if (containingNamespace == null || containingNamespace.IndexOf(namespaceFilter, StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                        }

                        var lineCount = SyntaxMetrics.GetMethodLineCount(method);
                        if (lineCount < minLines)
                            continue;

                        // Extract tokens for comparison (ignore trivia/whitespace)
                        var tokens = ExtractMethodTokens(method);
                        if (tokens.Length == 0)
                            continue;

                        methodInfos.Add(new MethodInfo(project.Name, methodSymbol, method, tokens));
                    }
                }
            }

            // Group methods by similar token counts to reduce comparisons (±30% size difference)
            var buckets = new Dictionary<int, List<int>>();
            for (int i = 0; i < methodInfos.Count; i++)
            {
                var tokenCount = methodInfos[i].Tokens.Length;
                var bucketKey = tokenCount / 10; // Group by 10s

                if (!buckets.ContainsKey(bucketKey))
                    buckets[bucketKey] = new List<int>();
                buckets[bucketKey].Add(i);
            }

            // Compare methods within same bucket
            foreach (var bucket in buckets.Values)
            {
                for (int i = 0; i < bucket.Count; i++)
                {
                    for (int j = i + 1; j < bucket.Count; j++)
                    {
                        var idx1 = bucket[i];
                        var idx2 = bucket[j];

                        var method1 = methodInfos[idx1];
                        var method2 = methodInfos[idx2];

                        // Skip if methods are too different in size (early filter)
                        var sizeDiff = Math.Abs(method1.Tokens.Length - method2.Tokens.Length);
                        var maxSize = Math.Max(method1.Tokens.Length, method2.Tokens.Length);
                        if (maxSize > 0 && (double)sizeDiff / maxSize > 0.3)
                            continue;

                        // Skip comparing a method to itself
                        if (method1.Symbol.Equals(method2.Symbol))
                            continue;

                        var similarity = CalculateTokenSimilarityFast(method1.Tokens, method2.Tokens, minSimilarity);
                        if (similarity >= minSimilarity)
                        {
                            var lineCount = Math.Min(
                                SyntaxMetrics.GetMethodLineCount(method1.Syntax),
                                SyntaxMetrics.GetMethodLineCount(method2.Syntax));

                            duplicates.Add(new DuplicateCodeInfo
                            {
                                Original = CreateDuplicateLocation(method1.ProjectName, method1.Symbol, method1.Syntax),
                                Duplicate = CreateDuplicateLocation(method2.ProjectName, method2.Symbol, method2.Syntax),
                                SimilarityPercent = similarity,
                                LineCount = lineCount,
                                TokenCount = Math.Min(method1.Tokens.Length, method2.Tokens.Length),
                                Message = $"{similarity}% similar code in {method1.Symbol.Name} and {method2.Symbol.Name}"
                            });
                        }
                    }
                }
            }

            // Sort by similarity descending
            return duplicates.OrderByDescending(d => d.SimilarityPercent).ToList();
        }

        private static string[] ExtractMethodTokens(MethodDeclarationSyntax method)
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

        /// <summary>
        /// Fast similarity calculation with early termination.
        /// Uses space-optimized LCS with two rows instead of full 2D array.
        /// </summary>
        private static int CalculateTokenSimilarityFast(string[] tokens1, string[] tokens2, int minSimilarity)
        {
            if (tokens1.Length == 0 || tokens2.Length == 0)
                return 0;

            // Quick exact match check
            if (tokens1.Length == tokens2.Length)
            {
                bool exactMatch = true;
                for (int i = 0; i < tokens1.Length; i++)
                {
                    if (tokens1[i] != tokens2[i])
                    {
                        exactMatch = false;
                        break;
                    }
                }
                if (exactMatch) return 100;
            }

            // Use space-optimized LCS (O(min(m,n)) space instead of O(m*n))
            var lcsLength = LongestCommonSubsequenceOptimized(tokens1, tokens2);
            var maxLength = Math.Max(tokens1.Length, tokens2.Length);

            return (int)((double)lcsLength / maxLength * 100);
        }

        /// <summary>
        /// Space-optimized LCS that uses O(min(m,n)) space instead of O(m*n).
        /// </summary>
        private static int LongestCommonSubsequenceOptimized(string[] seq1, string[] seq2)
        {
            // Ensure seq1 is the shorter sequence to minimize space usage
            if (seq1.Length > seq2.Length)
            {
                var temp = seq1;
                seq1 = seq2;
                seq2 = temp;
            }

            int m = seq1.Length;
            int n = seq2.Length;

            // Use only two rows instead of full 2D array
            var prev = new int[m + 1];
            var curr = new int[m + 1];

            for (int j = 1; j <= n; j++)
            {
                for (int i = 1; i <= m; i++)
                {
                    if (seq1[i - 1] == seq2[j - 1])
                        curr[i] = prev[i - 1] + 1;
                    else
                        curr[i] = Math.Max(curr[i - 1], prev[i]);
                }

                // Swap rows
                var temp = prev;
                prev = curr;
                curr = temp;
                Array.Clear(curr, 0, curr.Length);
            }

            return prev[m];
        }

        private static DuplicateLocation CreateDuplicateLocation(string projectName, IMethodSymbol symbol, MethodDeclarationSyntax syntax)
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

        /// <summary>
        /// Internal class for tracking method information during analysis.
        /// </summary>
        private class MethodInfo
        {
            public string ProjectName { get; }
            public IMethodSymbol Symbol { get; }
            public MethodDeclarationSyntax Syntax { get; }
            public string[] Tokens { get; }

            public MethodInfo(string projectName, IMethodSymbol symbol, MethodDeclarationSyntax syntax, string[] tokens)
            {
                ProjectName = projectName;
                Symbol = symbol;
                Syntax = syntax;
                Tokens = tokens;
            }
        }
    }
}
