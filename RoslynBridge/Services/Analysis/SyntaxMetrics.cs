#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynBridge.Models;

namespace RoslynBridge.Services.Analysis
{
    /// <summary>
    /// Provides utility methods for calculating metrics from Roslyn syntax trees.
    /// </summary>
    public static class SyntaxMetrics
    {
        /// <summary>
        /// Gets the line count of a method body.
        /// </summary>
        public static int GetMethodLineCount(MethodDeclarationSyntax method)
        {
            if (method.Body == null && method.ExpressionBody == null)
                return 0;

            var span = method.Body?.Span ?? method.ExpressionBody!.Span;
            var text = method.SyntaxTree.GetText();
            var startLine = text.Lines.GetLineFromPosition(span.Start).LineNumber;
            var endLine = text.Lines.GetLineFromPosition(span.End).LineNumber;

            return endLine - startLine + 1;
        }

        /// <summary>
        /// Gets the line count of a class declaration.
        /// </summary>
        public static int GetClassLineCount(ClassDeclarationSyntax classDecl)
        {
            var span = classDecl.Span;
            var text = classDecl.SyntaxTree.GetText();
            var startLine = text.Lines.GetLineFromPosition(span.Start).LineNumber;
            var endLine = text.Lines.GetLineFromPosition(span.End).LineNumber;

            return endLine - startLine + 1;
        }

        /// <summary>
        /// Creates a LocationInfo from a Roslyn Location.
        /// </summary>
        public static LocationInfo CreateLocationInfo(Location location)
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

        /// <summary>
        /// Determines if a file path indicates a generated file that should be skipped.
        /// </summary>
        public static bool IsGeneratedFile(string? filePath)
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
    }
}
