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
        messageFormat: "Type '{0}' is not an interface. Only interface types can be used as inline interface targets.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor TargetTypeCannotBeNullableRule = new(
        id: "MII0002",
        title: "Target type cannot be nullable",
        messageFormat: "Type '{0}' is nullable. Nullable interface types are not supported. Use the non-nullable version instead.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedPropertyMemberRule = new(
        id: "MII0003",
        title: "Property members are not allowed",
        messageFormat: "Property '{1}' is not allowed in target interface '{0}'. Inline interfaces only support method members.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedEventMemberRule = new(
        id: "MII0004",
        title: "Event members are not allowed",
        messageFormat: "Event '{1}' is not allowed in target interface '{0}'. Inline interfaces only support method members.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedGenericMethodRule = new(
        id: "MII0005",
        title: "Generic methods are not allowed",
        messageFormat: "Generic method '{1}' is not allowed in target interface '{0}'. Inline interfaces do not support generic methods.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedMethodModifierRule = new(
        id: "MII0006",
        title: "Method parameter modifiers are not allowed",
        messageFormat: "Method '{1}' in target interface '{0}' has unsupported parameter modifiers (ref, out, in, or params). Only value and reference parameters are supported.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor UnexpectedMemberTypeRule = new(
        id: "MII0007",
        title: "Unexpected member type",
        messageFormat: "Unexpected member '{2}' of type '{1}' found in target interface '{0}'. This member will be ignored.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
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
        var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var member in typeSymbol.GetMembers())
        {
            switch (member)
            {
                case IPropertySymbol { IsStatic: false } property:
                {
                    diagnosticsBuilder.Add(Diagnostic.Create(
                        descriptor: NotAllowedPropertyMemberRule,
                        location: typeArgument.GetLocation(),
                        messageArgs: [typeArgument, property.Name]
                    ));

                    break;
                }
                case IEventSymbol { IsStatic: false } @event:
                {
                    diagnosticsBuilder.Add(Diagnostic.Create(
                        descriptor: NotAllowedEventMemberRule,
                        location: typeArgument.GetLocation(),
                        messageArgs: [typeArgument, @event.Name]
                    ));

                    break;
                }
                case IMethodSymbol { IsStatic: false } method:
                {
                    // 제네릭 메서드 체크
                    if (method.IsGenericMethod)
                    {
                        diagnosticsBuilder.Add(Diagnostic.Create(
                            descriptor: NotAllowedGenericMethodRule,
                            location: typeArgument.GetLocation(),
                            messageArgs: [typeArgument, method.Name]
                        ));

                        break;
                    }

                    if (method.Parameters.Any(paramSymbol =>
                    {
                        return paramSymbol.RefKind != RefKind.None || paramSymbol.IsParams;
                    }))
                    {
                        diagnosticsBuilder.Add(Diagnostic.Create(
                            descriptor: NotAllowedMethodModifierRule,
                            location: typeArgument.GetLocation(),
                            messageArgs: [typeArgument, method.Name]
                        ));

                        break;
                    }

                    methodSymbolsBuilder.Add(method);

                    break;
                }
                case { IsStatic: true }:
                {
                    break;
                }
                default:
                {
                    diagnosticsBuilder.Add(Diagnostic.Create(
                        descriptor: UnexpectedMemberTypeRule,
                        location: typeArgument.GetLocation(),
                        messageArgs: [typeArgument, member.Kind, member.Name]
                    ));

                    break;
                }
            }
        }

        if (diagnosticsBuilder.Count > 0)
        {
            return TypeContext.Empty with
            {
                Diagnostics = diagnosticsBuilder.ToImmutable(),
            };
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
        var uniqueTypeContexts = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        var valueProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is InvocationExpressionSyntax,
                transform: static (generatorSyntaxContext, _) => GetTypeContext(generatorSyntaxContext)
            )
            .Where(static typeContext => typeContext != TypeContext.Empty)
            .Where(typeContext => typeContext switch
            {
                ImplementationOfTypeContext { Symbol: { } symbol } => uniqueTypeContexts.Add(symbol),
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
