using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.InlineInterface.InterfaceValidationIssueKind;

namespace Macaron.InlineInterface;

public readonly record struct InterfaceValidationIssue(
    InterfaceValidationIssueKind Kind,
    string MemberName,
    SymbolKind MemberKind = default
)
{
    public Diagnostic CreateDiagnostic(TypeSyntax typeSyntax)
    {
        return Kind switch
        {
            NotAllowedEventModifier => InlineInterfaceDiagnosticFactory.NotAllowedEventModifier(
                typeSyntax,
                MemberName
            ),
            NotAllowedGenericMethod => InlineInterfaceDiagnosticFactory.NotAllowedGenericMethod(
                typeSyntax,
                MemberName
            ),
            NotAllowedMethodModifier => InlineInterfaceDiagnosticFactory.NotAllowedMethodModifier(
                typeSyntax,
                MemberName
            ),
            NotAllowedStaticAbstractMember => InlineInterfaceDiagnosticFactory.NotAllowedStaticAbstractMember(
                typeSyntax,
                MemberName
            ),
            UnexpectedMemberType => InlineInterfaceDiagnosticFactory.UnexpectedMemberType(
                typeSyntax,
                MemberKind,
                MemberName
            ),
            _ => throw new InvalidOperationException("Unexpected interface validation issue."),
        };
    }
}
