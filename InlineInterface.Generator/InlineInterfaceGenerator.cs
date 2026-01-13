using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.InlineInterface.SourceGenerationHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

[Generator]
public sealed class InlineInterfaceGenerator : IIncrementalGenerator
{
    #region Constants
    private const string ImplementationTypeString = "global::Macaron.InlineInterface.Implementation";
    #endregion

    #region Types
    public record TypeContext(
        ImmutableArray<Diagnostic> Diagnostics
    )
    {
        #region Static
        public static readonly TypeContext Empty = new(
            Diagnostics: ImmutableArray<Diagnostic>.Empty
        );
        #endregion
    }

    public sealed record ImplementationOfTypeContext(
        INamedTypeSymbol Symbol,
        ImmutableArray<IMethodSymbol> MethodSymbols,
        ImmutableArray<Diagnostic> Diagnostics
    ) : TypeContext(Diagnostics);
    #endregion

    #region Diagnostics
    private static readonly DiagnosticDescriptor TargetTypeMustBeInterfaceRule = new(
        id: "MII0001",
        title: "Target type must be interface",
        messageFormat: "Type '{0}' is not interface. Only interface types can be used as targets.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor TargetTypeCannotBeNullableRule = new(
        id: "MII0002",
        title: "Target type cannot be nullable",
        messageFormat: "Type '{0}' is nullable. Nullable types are not supported as targets.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedPropertyMemberRule = new(
        id: "MII0003",
        title: "Property members are not allowed",
        messageFormat: "Property members are not allowed in target type '{0}'.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedEventMemberRule = new(
        id: "MII0004",
        title: "Event members are not allowed",
        messageFormat: "Event members are not allowed in target type '{0}'.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedGenericMethodRule = new(
        id: "MII0005",
        title: "Generic methods are not allowed",
        messageFormat: "Generic methods are not allowed in target type '{0}'.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedMethodModifierRule = new(
        id: "MII0006",
        title: "Method modifiers are not allowed",
        messageFormat: "Method modifiers are not allowed in target type '{0}'.",
        category: "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    #endregion

    #region Static
    public static TypeContext GetTypeContext(GeneratorSyntaxContext generatorSyntaxContext)
    {
        if (generatorSyntaxContext.Node is not InvocationExpressionSyntax expressionSyntax)
        {
            return TypeContext.Empty;
        }

        if (GetGenericNameFromInvocation(expressionSyntax) is not { } genericNameSyntax)
        {
            return TypeContext.Empty;
        }

        var semanticModel = generatorSyntaxContext.SemanticModel;
        var methodSymbol = semanticModel.GetSymbolInfo(genericNameSyntax).Symbol as IMethodSymbol;
        if (methodSymbol?.IsStatic is not true || methodSymbol.Name != "Of")
        {
            return TypeContext.Empty;
        }

        var typeArgumentList = genericNameSyntax.TypeArgumentList;
        if (typeArgumentList.Arguments is not [{ } typeArgument] ||
            semanticModel.GetSymbolInfo(typeArgument).Symbol is not INamedTypeSymbol { ConstructedFrom: { } typeSymbol }
        )
        {
            return TypeContext.Empty;
        }

        if (methodSymbol.ContainingType.ToDisplayString(FullyQualifiedFormat) != ImplementationTypeString)
        {
            return TypeContext.Empty;
        }

        if (typeSymbol.TypeKind != TypeKind.Interface)
        {
            return TypeContext.Empty with
            {
                Diagnostics = ImmutableArray.Create(Diagnostic.Create(
                    descriptor: TargetTypeMustBeInterfaceRule,
                    location: typeArgument.GetLocation(),
                    messageArgs: [typeArgument]
                )),
            };
        }

        if (typeArgument.ToString().EndsWith("?"))
        {
            return TypeContext.Empty with
            {
                Diagnostics = ImmutableArray.Create(Diagnostic.Create(
                    descriptor: TargetTypeCannotBeNullableRule,
                    location: typeArgument.GetLocation(),
                    messageArgs: [typeArgument]
                )),
            };
        }

        var methodSymbolsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var member in typeSymbol.GetMembers())
        {
            switch (member)
            {
                case IPropertySymbol { IsStatic: false }:
                {
                    return TypeContext.Empty with
                    {
                        Diagnostics = ImmutableArray.Create(Diagnostic.Create(
                            descriptor: NotAllowedPropertyMemberRule,
                            location: typeArgument.GetLocation(),
                            messageArgs: [typeArgument]
                        )),
                    };
                }
                case IEventSymbol { IsStatic: false }:
                {
                    return TypeContext.Empty with
                    {
                        Diagnostics = ImmutableArray.Create(Diagnostic.Create(
                            descriptor: NotAllowedEventMemberRule,
                            location: typeArgument.GetLocation(),
                            messageArgs: [typeArgument]
                        )),
                    };
                }
                case IMethodSymbol { IsStatic: false } symbol:
                {
                    if (symbol.IsGenericMethod)
                    {
                        return TypeContext.Empty with
                        {
                            Diagnostics = ImmutableArray.Create(Diagnostic.Create(
                                descriptor: NotAllowedGenericMethodRule,
                                location: typeArgument.GetLocation(),
                                messageArgs: [typeArgument]
                            )),
                        };
                    }

                    if (symbol.Parameters.Any(parameterSymbol =>
                    {
                        return parameterSymbol.RefKind != RefKind.None || parameterSymbol.IsParams;
                    }))
                    {
                        return TypeContext.Empty with
                        {
                            Diagnostics = ImmutableArray.Create(Diagnostic.Create(
                                descriptor: NotAllowedMethodModifierRule,
                                location: typeArgument.GetLocation(),
                                messageArgs: [typeArgument]
                            )),
                        };
                    }

                    methodSymbolsBuilder.Add(symbol);

                    break;
                }
                default:
                    throw new InvalidOperationException($"Unexpected member type: {member.Kind}");
            }
        }

        return new ImplementationOfTypeContext(
            Symbol: typeSymbol,
            MethodSymbols: methodSymbolsBuilder.ToImmutable(),
            Diagnostics: ImmutableArray<Diagnostic>.Empty
        );

        #region Local Functions
        static GenericNameSyntax? GetGenericNameFromInvocation(InvocationExpressionSyntax invocationExpressionSyntax)
        {
            return invocationExpressionSyntax.Expression switch
            {
                MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName } => genericName,
                GenericNameSyntax genericName => genericName,
                _ => null,
            };
        }
        #endregion
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeContexts = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        var valueProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is InvocationExpressionSyntax,
                transform: static (generatorSyntaxContext, _) => GetTypeContext(generatorSyntaxContext)
            )
            .Where(static typeContext => typeContext != TypeContext.Empty)
            .Where(typeContext => typeContext switch
            {
                ImplementationOfTypeContext { Symbol: { } symbol } => typeContexts.Add(symbol),
                _ => true,
            })
            .Collect();

        context.RegisterSourceOutput(
            source: valueProvider,
            action: (sourceProductionContext, typeContexts) =>
            {
                foreach (var diagnostic in typeContexts.SelectMany(static context => context.Diagnostics))
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }

                foreach (var typeContext in typeContexts.OfType<ImplementationOfTypeContext>())
                {
                    AddSource(
                        context: sourceProductionContext,
                        typeSymbol: typeContext.Symbol,
                        methodSymbols: typeContext.MethodSymbols
                    );
                }
            });
    }
    #endregion
}
