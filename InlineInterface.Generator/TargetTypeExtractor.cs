using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.Accessibility;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

internal static class TargetTypeExtractor
{
    private const string ImplementationTypeString = "global::Macaron.InlineInterface.Implementation";

    public static TargetTypeDiscoveryResult Discover(GeneratorSyntaxContext generatorSyntaxContext)
    {
        if (generatorSyntaxContext.Node is not InvocationExpressionSyntax expressionSyntax)
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        if (GetGenericNameFromInvocation(expressionSyntax) is not { } genericNameSyntax)
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        var semanticModel = generatorSyntaxContext.SemanticModel;
        var methodSymbol = semanticModel.GetSymbolInfo(genericNameSyntax).Symbol as IMethodSymbol;

        if (methodSymbol?.IsStatic is not true || methodSymbol.Name != "Of")
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        var typeArgumentList = genericNameSyntax.TypeArgumentList;
        if (typeArgumentList.Arguments is not [{ } typeArgumentSyntax])
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        if (semanticModel.GetSymbolInfo(typeArgumentSyntax).Symbol is not INamedTypeSymbol
        {
            OriginalDefinition: { } typeSymbol,
        })
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        if (methodSymbol.ContainingType.ToDisplayString(FullyQualifiedFormat) != ImplementationTypeString)
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        if (typeSymbol.TypeKind != TypeKind.Interface)
        {
            return new TargetTypeDiscoveryResult.Failure(
                Diagnostic: InlineInterfaceDiagnosticFactory.TargetTypeMustBeInterface(typeArgumentSyntax)
            );
        }

        if (typeArgumentSyntax.ToString().EndsWith("?"))
        {
            return new TargetTypeDiscoveryResult.Failure(
                Diagnostic: InlineInterfaceDiagnosticFactory.TargetTypeCannotBeNullable(typeArgumentSyntax)
            );
        }

        if (!IsAccessibleFromGeneratedCode(typeSymbol))
        {
            return new TargetTypeDiscoveryResult.Failure(
                Diagnostic: InlineInterfaceDiagnosticFactory.TargetTypeMustBeAccessible(typeArgumentSyntax)
            );
        }

        return new TargetTypeDiscoveryResult.Success(
            Symbol: typeSymbol,
            Syntax: typeArgumentSyntax
        );
    }

    private static GenericNameSyntax? GetGenericNameFromInvocation(InvocationExpressionSyntax invocationExpressionSyntax)
    {
        return invocationExpressionSyntax.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName } => genericName,
            GenericNameSyntax genericName => genericName,
            _ => null,
        };
    }

    private static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol)
    {
        var current = typeSymbol;

        while (current is not null)
        {
            if (!IsAllowedAccessibility(current.DeclaredAccessibility))
            {
                return false;
            }

            current = current.ContainingType;
        }

        return true;
    }

    private static bool IsAllowedAccessibility(Accessibility accessibility)
    {
        return accessibility is Public or Internal or ProtectedOrInternal;
    }
}
