using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CrunchEconV3.Utils
{
    class FactionStationRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var parameters = node.ParameterList.Parameters;
            bool changed = false;

            var newParams = new List<ParameterSyntax>();

            foreach (var param in parameters)
            {
                if (param.Type?.ToString().EndsWith("MyStation") == true)
                {
                    var newParam = param.WithType(
                        SyntaxFactory.ParseTypeName("IMyFactionStation"));

                    newParams.Add(newParam);
                    changed = true;
                }
                else
                {
                    newParams.Add(param);
                }
            }

            if (changed)
            {
                node = node.WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SeparatedList(newParams)));
            }

            return base.VisitMethodDeclaration(node);
        }
    }
}
