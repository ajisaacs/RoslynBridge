#nullable enable
using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynBridge.Services.Analysis
{
    /// <summary>
    /// Calculates maximum nesting depth in a method by walking its syntax tree.
    /// Tracks depth of nested control flow statements.
    /// </summary>
    public class NestingDepthCalculator : CSharpSyntaxWalker
    {
        private int _currentDepth;

        /// <summary>
        /// The maximum nesting depth found in the visited syntax.
        /// </summary>
        public int MaxDepth { get; private set; }

        /// <summary>
        /// Calculates the maximum nesting depth of the given method.
        /// </summary>
        public static int Calculate(MethodDeclarationSyntax method)
        {
            var calculator = new NestingDepthCalculator();
            calculator.Visit(method);
            return calculator.MaxDepth;
        }

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
}
