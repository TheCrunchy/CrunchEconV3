using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconV3.Utils
{
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis;

    public class BuildUnassignedContractRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            node = (ObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node);

            if (node.Initializer == null)
                return node;

            var expressions = node.Initializer.Expressions;

            // Check if DefinitionId assignment exists
            var definitionAssignment = expressions
                .OfType<AssignmentExpressionSyntax>()
                .FirstOrDefault(a =>
                    a.Left.ToString() == "DefinitionId" &&
                    a.Right.ToString() == "definitionId");

            if (definitionAssignment == null)
                return node;

            // Avoid duplicate insertion
            bool alreadyExists = expressions
                .OfType<AssignmentExpressionSyntax>()
                .Any(a => a.Left.ToString() == "ContractTypeDefinitionId");

            if (alreadyExists)
                return node;

            // Create:
            // ContractTypeDefinitionId = definitionId
            var newAssignment =
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName("ContractTypeDefinitionId"),
                    SyntaxFactory.IdentifierName("definitionId"));

            // Insert immediately after DefinitionId
            var newExpressions = new SeparatedSyntaxList<ExpressionSyntax>();

            foreach (var expr in expressions)
            {
                newExpressions = newExpressions.Add(expr);

                if (expr == definitionAssignment)
                {
                    newExpressions = newExpressions.Add(newAssignment);
                }
            }

            return node.WithInitializer(
                node.Initializer.WithExpressions(newExpressions));
        }
    }
}
