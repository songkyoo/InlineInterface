using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.Accessibility;

namespace Macaron.InlineInterface;

internal static class TargetTypeExtractor
{
    public static bool IsCandidate(SyntaxNode syntaxNode)
    {
        return GetCandidateGenericName(syntaxNode) is not null;
    }

    public static TargetTypeDiscoveryResult Discover(GeneratorSyntaxContext generatorSyntaxContext)
    {
        if (GetCandidateGenericName(generatorSyntaxContext.Node) is not { } genericNameSyntax)
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        var semanticModel = generatorSyntaxContext.SemanticModel;
        var methodSymbol = semanticModel.GetSymbolInfo(genericNameSyntax).Symbol as IMethodSymbol;

        if (methodSymbol?.IsStatic is not true ||
            methodSymbol.Name != "Of" ||
            !IsImplementationType(methodSymbol.ContainingType)
        )
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        var typeArgumentSyntax = genericNameSyntax.TypeArgumentList.Arguments[0];

        if (semanticModel.GetSymbolInfo(typeArgumentSyntax).Symbol is not INamedTypeSymbol
            {
                OriginalDefinition: { } typeSymbol,
            }
        )
        {
            return new TargetTypeDiscoveryResult.NotApplicable();
        }

        if (typeSymbol.TypeKind != TypeKind.Interface)
        {
            return new TargetTypeDiscoveryResult.Failure(
                InlineInterfaceDiagnosticFactory.TargetTypeMustBeInterface(typeArgumentSyntax)
            );
        }

        if (typeArgumentSyntax.ToString().EndsWith("?"))
        {
            return new TargetTypeDiscoveryResult.Failure(
                InlineInterfaceDiagnosticFactory.TargetTypeCannotBeNullable(typeArgumentSyntax)
            );
        }

        if (!IsAccessibleFromGeneratedCode(typeSymbol))
        {
            return new TargetTypeDiscoveryResult.Failure(
                InlineInterfaceDiagnosticFactory.TargetTypeMustBeAccessible(typeArgumentSyntax)
            );
        }

        return new TargetTypeDiscoveryResult.Success(
            Symbol: typeSymbol,
            Syntax: typeArgumentSyntax
        );
    }

    public static GenericNameSyntax? GetCandidateGenericName(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not InvocationExpressionSyntax invocationExpressionSyntax)
        {
            return null;
        }

        var genericNameSyntax = invocationExpressionSyntax.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName } => genericName,
            GenericNameSyntax genericName => genericName,
            _ => null,
        };

        return genericNameSyntax is
            {
                Identifier.ValueText: "Of",
                TypeArgumentList.Arguments.Count: 1,
            }
            ? genericNameSyntax
            : null;
    }

    public static bool IsImplementationType(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol is
        {
            Name: "Implementation",
            Arity: 0,
            ContainingType: null,
            ContainingNamespace:
            {
                Name: "InlineInterface",
                ContainingNamespace:
                {
                    Name: "Macaron",
                    ContainingNamespace.IsGlobalNamespace: true,
                },
            },
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
