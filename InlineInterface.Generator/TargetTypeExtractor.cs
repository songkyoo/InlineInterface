using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.Accessibility;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using static Microsoft.CodeAnalysis.TypeKind;

namespace Macaron.InlineInterface;

public static class TargetTypeExtractor
{
    public static bool IsCandidate(SyntaxNode syntaxNode)
    {
        return GetCandidateGenericName(syntaxNode) is not null;
    }

    public static TargetTypeDiscoveryResult Discover(
        GeneratorSyntaxContext generatorSyntaxContext,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (GetCandidateGenericName(generatorSyntaxContext.Node) is not { } genericNameSyntax)
        {
            return TargetTypeDiscoveryResult.NotApplicable.Instance;
        }

        var semanticModel = generatorSyntaxContext.SemanticModel;
        var symbolInfo = semanticModel.GetSymbolInfo(genericNameSyntax, cancellationToken);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (!IsTargetMethod(methodSymbol))
        {
            methodSymbol = null;

            foreach (var candidateSymbol in symbolInfo.CandidateSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (candidateSymbol is IMethodSymbol candidateMethod && IsTargetMethod(candidateMethod))
                {
                    methodSymbol = candidateMethod;

                    break;
                }
            }

            if (methodSymbol is null)
            {
                return TargetTypeDiscoveryResult.NotApplicable.Instance;
            }
        }

        var typeArgumentSyntax = genericNameSyntax.TypeArgumentList.Arguments[0];

        if (methodSymbol!.TypeArguments is not [INamedTypeSymbol { OriginalDefinition: { } typeSymbol }])
        {
            return TargetTypeDiscoveryResult.NotApplicable.Instance;
        }

        if (typeSymbol.TypeKind != Interface)
        {
            return new TargetTypeDiscoveryResult.Failure(
                InlineInterfaceDiagnosticFactory.TargetTypeMustBeInterface(typeArgumentSyntax)
            );
        }

        if (typeArgumentSyntax.IsKind(NullableType))
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

    private static bool IsTargetMethod(IMethodSymbol? methodSymbol)
    {
        return methodSymbol is { IsStatic: true, Name: "Of" } && IsImplementationType(methodSymbol.ContainingType);
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
