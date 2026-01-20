#nullable enable
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynBridge.Services.Analysis
{
    /// <summary>
    /// Calculates cyclomatic complexity of a method by walking its syntax tree.
    /// Complexity increases for each branching or decision point.
    /// </summary>
    public class ComplexityCalculator : CSharpSyntaxWalker
    {
        /// <summary>
        /// The calculated cyclomatic complexity (starts at 1 for the base path).
        /// </summary>
        public int Complexity { get; private set; } = 1;

        /// <summary>
        /// Calculates the cyclomatic complexity of the given method.
        /// </summary>
        public static int Calculate(MethodDeclarationSyntax method)
        {
            var calculator = new ComplexityCalculator();
            calculator.Visit(method);
            return calculator.Complexity;
        }

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
}
