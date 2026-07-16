using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            InterfaceValidationIssueKind.NotAllowedEventModifier =>
                InlineInterfaceDiagnosticFactory.NotAllowedEventModifier(typeSyntax, MemberName),
            InterfaceValidationIssueKind.NotAllowedGenericMethod =>
                InlineInterfaceDiagnosticFactory.NotAllowedGenericMethod(typeSyntax, MemberName),
            InterfaceValidationIssueKind.NotAllowedMethodModifier =>
                InlineInterfaceDiagnosticFactory.NotAllowedMethodModifier(typeSyntax, MemberName),
            InterfaceValidationIssueKind.UnexpectedMemberType =>
                InlineInterfaceDiagnosticFactory.UnexpectedMemberType(typeSyntax, MemberKind, MemberName),
            _ => throw new InvalidOperationException("Unexpected interface validation issue."),
        };
    }
}

public enum InterfaceValidationIssueKind
{
    NotAllowedEventModifier,
    NotAllowedGenericMethod,
    NotAllowedMethodModifier,
    UnexpectedMemberType,
}
