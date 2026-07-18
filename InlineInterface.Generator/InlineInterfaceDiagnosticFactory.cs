using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface;

public static class InlineInterfaceDiagnosticFactory
{
    public static Diagnostic TargetTypeMustBeInterface(TypeSyntax typeSyntax)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.TargetTypeMustBeInterfaceRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax]
        );
    }

    public static Diagnostic TargetTypeCannotBeNullable(TypeSyntax typeSyntax)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.TargetTypeCannotBeNullableRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax]
        );
    }

    public static Diagnostic TargetTypeMustBeAccessible(TypeSyntax typeSyntax)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.TargetTypeMustBeAccessibleRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax]
        );
    }

    public static Diagnostic NotAllowedEventModifier(TypeSyntax typeSyntax, string eventName)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.NotAllowedEventModifierRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax, eventName]
        );
    }

    public static Diagnostic NotAllowedGenericMethod(TypeSyntax typeSyntax, string methodName)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.NotAllowedGenericMethodRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax, methodName]
        );
    }

    public static Diagnostic NotAllowedMethodModifier(TypeSyntax typeSyntax, string methodName)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.NotAllowedMethodModifierRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax, methodName]
        );
    }

    public static Diagnostic NotAllowedStaticAbstractMember(TypeSyntax typeSyntax, string memberName)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.NotAllowedStaticAbstractMemberRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax, memberName]
        );
    }

    public static Diagnostic UnexpectedMemberType(TypeSyntax typeSyntax, SymbolKind memberKind, string memberName)
    {
        return Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.UnexpectedMemberTypeRule,
            location: typeSyntax.GetLocation(),
            messageArgs: [typeSyntax, memberKind, memberName]
        );
    }
}
