using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.InlineInterface.SourceGenerationHelpers;
using static Microsoft.CodeAnalysis.Accessibility;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

[Generator]
public sealed class InlineInterfaceGenerator : IIncrementalGenerator
{
    #region Constants
    private const string ImplementationTypeString = "global::Macaron.InlineInterface.Implementation";
    #endregion

    #region Types
    private abstract record ExtractionResult
    {
        private ExtractionResult() { }

        public sealed record Success(
            INamedTypeSymbol Symbol,
            TypeSyntax Syntax
        ) : ExtractionResult;

        public sealed record Failure(
            Diagnostic Diagnostic
        ) : ExtractionResult;

        public sealed record NotApplicable : ExtractionResult;
    }

    private readonly struct InterfaceContextOrDiagnostics
    {
        private readonly int _index;
        private readonly InterfaceContext? _interfaceContext;
        private readonly ImmutableArray<Diagnostic> _diagnostics;

        public InterfaceContextOrDiagnostics(InterfaceContext interfaceContext)
        {
            _index = 1;
            _interfaceContext = interfaceContext;
            _diagnostics = ImmutableArray<Diagnostic>.Empty;
        }

        public InterfaceContextOrDiagnostics(ImmutableArray<Diagnostic> diagnostics)
        {
            _index = 2;
            _interfaceContext = null;
            _diagnostics = diagnostics;
        }

        public bool TryGetInterfaceContext([NotNullWhen(returnValue: true)]out InterfaceContext? value)
        {
            if (_index == 1)
            {
                value = _interfaceContext!;

                return true;
            }
            else
            {
                value = null;

                return false;
            }
        }

        public bool TryGetDiagnostics(out ImmutableArray<Diagnostic> diagnostics)
        {
            if (_index == 2)
            {
                diagnostics = _diagnostics;

                return true;
            }
            else
            {
                diagnostics = default;

                return false;
            }
        }
    }

    private abstract record ValidationResult
    {
        public sealed record Success(
            INamedTypeSymbol InterfaceSymbol,
            ImmutableArray<InterfaceContext> Contexts
        ) : ValidationResult;

        public sealed record Failure(
            ImmutableArray<Diagnostic> Diagnostics
        ) : ValidationResult;
    }
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
    private static readonly DiagnosticDescriptor NotAllowedGenericMethodRule = new(
        id: "MII0003",
        title: "Generic methods are not allowed",
        messageFormat: "Generic method '{1}' is not allowed in target interface '{0}'. Inline interfaces do not support generic methods.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor NotAllowedMethodModifierRule = new(
        id: "MII0004",
        title: "Method parameter modifiers are not allowed",
        messageFormat: "Method '{1}' in target interface '{0}' has unsupported parameter modifiers (ref, out, in, or params). Only value and reference parameters are supported.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor UnexpectedMemberTypeRule = new(
        id: "MII0005",
        title: "Unexpected member type",
        messageFormat: "Unexpected member '{2}' of type '{1}' found in target interface '{0}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    private static readonly DiagnosticDescriptor TargetTypeMustBeAccessibleRule = new(
        id: "MII0006",
        title: "Target type must be accessible",
        messageFormat: "Type '{0}' is not accessible from generated code. Target interfaces and all containing types must be public, internal, or protected internal.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    #endregion

    #region Static
    private static ExtractionResult ExtractTypeSymbol(GeneratorSyntaxContext generatorSyntaxContext)
    {
        if (generatorSyntaxContext.Node is not InvocationExpressionSyntax expressionSyntax)
        {
            return new ExtractionResult.NotApplicable();
        }

        if (GetGenericNameFromInvocation(expressionSyntax) is not { } genericNameSyntax)
        {
            return new ExtractionResult.NotApplicable();
        }

        var semanticModel = generatorSyntaxContext.SemanticModel;
        var methodSymbol = semanticModel.GetSymbolInfo(genericNameSyntax).Symbol as IMethodSymbol;

        if (methodSymbol?.IsStatic is not true || methodSymbol.Name != "Of")
        {
            return new ExtractionResult.NotApplicable();
        }

        var typeArgumentList = genericNameSyntax.TypeArgumentList;
        if (typeArgumentList.Arguments is not [{ } typeArgumentSyntax])
        {
            return new ExtractionResult.NotApplicable();
        }

        if (semanticModel.GetSymbolInfo(typeArgumentSyntax).Symbol is not INamedTypeSymbol
        {
            OriginalDefinition: { } typeSymbol,
        })
        {
            return new ExtractionResult.NotApplicable();
        }

        if (methodSymbol.ContainingType.ToDisplayString(FullyQualifiedFormat) != ImplementationTypeString)
        {
            return new ExtractionResult.NotApplicable();
        }

        if (typeSymbol.TypeKind != TypeKind.Interface)
        {
            return new ExtractionResult.Failure(
                Diagnostic: Diagnostic.Create(
                    descriptor: TargetTypeMustBeInterfaceRule,
                    location: typeArgumentSyntax.GetLocation(),
                    messageArgs: [typeArgumentSyntax]
                )
            );
        }

        if (typeArgumentSyntax.ToString().EndsWith("?"))
        {
            return new ExtractionResult.Failure(
                Diagnostic: Diagnostic.Create(
                    descriptor: TargetTypeCannotBeNullableRule,
                    location: typeArgumentSyntax.GetLocation(),
                    messageArgs: [typeArgumentSyntax]
                )
            );
        }

        if (!IsAccessibleFromGeneratedCode(typeSymbol))
        {
            return new ExtractionResult.Failure(
                Diagnostic: Diagnostic.Create(
                    descriptor: TargetTypeMustBeAccessibleRule,
                    location: typeArgumentSyntax.GetLocation(),
                    messageArgs: [typeArgumentSyntax]
                )
            );
        }

        return new ExtractionResult.Success(
            Symbol: typeSymbol,
            Syntax: typeArgumentSyntax
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

        static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol typeSymbol)
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

            #region Local Functions
            static bool IsAllowedAccessibility(Accessibility accessibility)
            {
                return accessibility is Public or Internal or ProtectedOrInternal;
            }
            #endregion
        }
        #endregion
    }

    private static ValidationResult ValidateTypeSymbol(INamedTypeSymbol interfaceSymbol, TypeSyntax typeSyntax)
    {
        var interfaceContextsBuilder = ImmutableArray.CreateBuilder<InterfaceContext>();
        var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var symbol in new[] { interfaceSymbol }.Concat(interfaceSymbol.AllInterfaces))
        {
            var result = Validate(symbol, typeSyntax);

            if (result.TryGetInterfaceContext(out var interfaceContext))
            {
                interfaceContextsBuilder.Add(interfaceContext);
            }
            else if (result.TryGetDiagnostics(out var diagnostics))
            {
                diagnosticsBuilder.AddRange(diagnostics);
            }
        }

        return diagnosticsBuilder.Count > 0
            ? new ValidationResult.Failure(Diagnostics: diagnosticsBuilder.ToImmutable())
            : new ValidationResult.Success(
                InterfaceSymbol: interfaceSymbol,
                Contexts: interfaceContextsBuilder.ToImmutable()
            );

        #region Local Functions
        static InterfaceContextOrDiagnostics Validate(INamedTypeSymbol interfaceSymbol, TypeSyntax typeSyntax)
        {
            var eventSymbolsBuilder = ImmutableArray.CreateBuilder<IEventSymbol>();
            var propertySymbolsBuilder = ImmutableArray.CreateBuilder<IPropertySymbol>();
            var methodSymbolsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
            var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();

            foreach (var member in interfaceSymbol.GetMembers())
            {
                switch (member)
                {
                    case IPropertySymbol { IsStatic: false } property:
                    {
                        propertySymbolsBuilder.Add(property);

                        break;
                    }
                    case IEventSymbol { IsStatic: false } @event:
                    {
                        eventSymbolsBuilder.Add(@event);

                        break;
                    }
                    case IMethodSymbol { IsStatic: false } method:
                    {
                        if (method.MethodKind
                            is MethodKind.EventAdd or MethodKind.EventRemove
                            or MethodKind.PropertyGet or MethodKind.PropertySet
                        )
                        {
                            break;
                        }

                        if (method.IsGenericMethod)
                        {
                            diagnosticsBuilder.Add(Diagnostic.Create(
                                descriptor: NotAllowedGenericMethodRule,
                                location: typeSyntax.GetLocation(),
                                messageArgs: [typeSyntax, method.Name]
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
                                location: typeSyntax.GetLocation(),
                                messageArgs: [typeSyntax, method.Name]
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
                            location: typeSyntax.GetLocation(),
                            messageArgs: [typeSyntax, member.Kind, member.Name]
                        ));

                        break;
                    }
                }
            }

            if (diagnosticsBuilder.Count > 0)
            {
                return new InterfaceContextOrDiagnostics(diagnostics : diagnosticsBuilder.ToImmutable());
            }
            else
            {
                return new InterfaceContextOrDiagnostics(interfaceContext: new InterfaceContext(
                        TypeSymbol: interfaceSymbol,
                        EventSymbols: eventSymbolsBuilder.ToImmutable(),
                        PropertySymbols: propertySymbolsBuilder.ToImmutable(),
                        MethodSymbols: methodSymbolsBuilder.ToImmutable()
                ));
            }
        }
        #endregion
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeSymbolProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is InvocationExpressionSyntax,
                transform: static (generatorSyntaxContext, _) => ExtractTypeSymbol(generatorSyntaxContext)
            )
            .Where(static result => result is not ExtractionResult.NotApplicable);

        var validatedProvider = typeSymbolProvider
            .Collect()
            .SelectMany(static (results, _) =>
            {
                var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var builder = ImmutableArray.CreateBuilder<ValidationResult>();

                foreach (var result in results)
                {
                    switch (result)
                    {
                        case ExtractionResult.Failure failure:
                        {
                            builder.Add(new ValidationResult.Failure(
                                Diagnostics: ImmutableArray.Create(failure.Diagnostic)
                            ));

                            break;
                        }
                        case ExtractionResult.Success success:
                        {
                            if (!seenTypes.Add(success.Symbol))
                            {
                                continue;
                            }

                            var validationResult = ValidateTypeSymbol(success.Symbol, success.Syntax);

                            builder.Add(validationResult);

                            break;
                        }
                    }
                }

                return builder.ToImmutable();
            });

        context.RegisterSourceOutput(
            source: validatedProvider,
            action: (sourceProductionContext, validationResult) =>
            {
                switch (validationResult)
                {
                    case ValidationResult.Failure failure:
                    {
                        foreach (var diagnostic in failure.Diagnostics)
                        {
                            sourceProductionContext.ReportDiagnostic(diagnostic);
                        }

                        break;
                    }
                    case ValidationResult.Success success:
                    {
                        var (interfaceSymbol, contexts) = success;

                        AddSource(sourceProductionContext, interfaceSymbol, contexts);

                        break;
                    }
                }
            }
        );
    }
    #endregion
}
