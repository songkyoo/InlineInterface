using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface;

internal static class InterfaceValidator
{
    public static InterfaceValidationResult ValidateTypeSymbol(INamedTypeSymbol interfaceSymbol, TypeSyntax typeSyntax)
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
            ? new InterfaceValidationResult.Failure(Diagnostics: diagnosticsBuilder.ToImmutable())
            : new InterfaceValidationResult.Success(
                InterfaceSymbol: interfaceSymbol,
                Contexts: interfaceContextsBuilder.ToImmutable()
            );
    }

    private static InterfaceContextOrDiagnostics Validate(INamedTypeSymbol interfaceSymbol, TypeSyntax typeSyntax)
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
                    if (@event.Type is INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod }
                        && invokeMethod.Parameters.Any(paramSymbol =>
                            (paramSymbol.RefKind != RefKind.None && paramSymbol.RefKind != RefKind.In)
                            || paramSymbol.IsParams))
                    {
                        diagnosticsBuilder.Add(InlineInterfaceDiagnosticFactory.NotAllowedEventModifier(typeSyntax, @event.Name));

                        break;
                    }

                    eventSymbolsBuilder.Add(@event);

                    break;
                }
                case IMethodSymbol { IsStatic: false } method:
                {
                    if (method.MethodKind
                        is MethodKind.EventAdd or MethodKind.EventRemove
                        or MethodKind.PropertyGet or MethodKind.PropertySet)
                    {
                        break;
                    }

                    if (method.IsGenericMethod)
                    {
                        diagnosticsBuilder.Add(InlineInterfaceDiagnosticFactory.NotAllowedGenericMethod(typeSyntax, method.Name));

                        break;
                    }

                    if (method.Parameters.Any(paramSymbol => paramSymbol.RefKind != RefKind.None || paramSymbol.IsParams))
                    {
                        diagnosticsBuilder.Add(InlineInterfaceDiagnosticFactory.NotAllowedMethodModifier(typeSyntax, method.Name));

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
                    diagnosticsBuilder.Add(InlineInterfaceDiagnosticFactory.UnexpectedMemberType(typeSyntax, member.Kind, member.Name));

                    break;
                }
            }
        }

        if (diagnosticsBuilder.Count > 0)
        {
            return new InterfaceContextOrDiagnostics(diagnostics: diagnosticsBuilder.ToImmutable());
        }

        return new InterfaceContextOrDiagnostics(interfaceContext: new InterfaceContext(
            TypeSymbol: interfaceSymbol,
            EventSymbols: eventSymbolsBuilder.ToImmutable(),
            PropertySymbols: propertySymbolsBuilder.ToImmutable(),
            MethodSymbols: methodSymbolsBuilder.ToImmutable()
        ));
    }
}
