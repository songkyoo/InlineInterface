using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface;

internal static class InterfaceValidator
{
    public static TargetInterfaceValidationResult ValidateTargetInterface(INamedTypeSymbol interfaceSymbol, TypeSyntax typeSyntax)
    {
        var result = ValidateTargetInterface(interfaceSymbol);

        if (result is TargetInterfaceSymbolValidationResult.Success success)
        {
            return new TargetInterfaceValidationResult.Success(
                InterfaceSymbol: success.InterfaceSymbol,
                Contexts: success.Contexts
            );
        }

        var failure = (TargetInterfaceSymbolValidationResult.Failure)result;
        var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>(failure.Issues.Length);

        foreach (var issue in failure.Issues)
        {
            diagnosticsBuilder.Add(issue.CreateDiagnostic(typeSyntax));
        }

        return new TargetInterfaceValidationResult.Failure(diagnosticsBuilder.ToImmutable());
    }

    public static TargetInterfaceSymbolValidationResult ValidateTargetInterface(INamedTypeSymbol interfaceSymbol)
    {
        var interfaceContextsBuilder = ImmutableArray.CreateBuilder<InterfaceContext>();
        var issuesBuilder = ImmutableArray.CreateBuilder<InterfaceValidationIssue>();

        foreach (var symbol in new[] { interfaceSymbol }.Concat(interfaceSymbol.AllInterfaces))
        {
            var result = ValidateInterfaceMembers(symbol);

            if (result.TryGetContext(out var interfaceContext))
            {
                interfaceContextsBuilder.Add(interfaceContext);
            }
            else if (result.TryGetIssues(out var issues))
            {
                issuesBuilder.AddRange(issues);
            }
        }

        return issuesBuilder.Count > 0
            ? new TargetInterfaceSymbolValidationResult.Failure(Issues: issuesBuilder.ToImmutable())
            : new TargetInterfaceSymbolValidationResult.Success(
                InterfaceSymbol: interfaceSymbol,
                Contexts: interfaceContextsBuilder.ToImmutable()
            );
    }

    private static InterfaceMemberValidationResult ValidateInterfaceMembers(INamedTypeSymbol interfaceSymbol)
    {
        var eventSymbolsBuilder = ImmutableArray.CreateBuilder<IEventSymbol>();
        var propertySymbolsBuilder = ImmutableArray.CreateBuilder<IPropertySymbol>();
        var methodSymbolsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
        var issuesBuilder = ImmutableArray.CreateBuilder<InterfaceValidationIssue>();

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
                        {
                            return paramSymbol.RefKind is not RefKind.None and not RefKind.In || paramSymbol.IsParams;
                        })
                    )
                    {
                        issuesBuilder.Add(new InterfaceValidationIssue(
                            Kind: InterfaceValidationIssueKind.NotAllowedEventModifier,
                            MemberName: @event.Name
                        ));

                        break;
                    }

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
                        issuesBuilder.Add(new InterfaceValidationIssue(
                            Kind: InterfaceValidationIssueKind.NotAllowedGenericMethod,
                            MemberName: method.Name
                        ));

                        break;
                    }

                    if (method.Parameters.Any(paramSymbol =>
                        {
                            return paramSymbol.RefKind != RefKind.None || paramSymbol.IsParams;
                        })
                    )
                    {
                        issuesBuilder.Add(new InterfaceValidationIssue(
                            Kind: InterfaceValidationIssueKind.NotAllowedMethodModifier,
                            MemberName: method.Name
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
                    issuesBuilder.Add(new InterfaceValidationIssue(
                        Kind: InterfaceValidationIssueKind.UnexpectedMemberType,
                        MemberName: member.Name,
                        MemberKind: member.Kind
                    ));

                    break;
                }
            }
        }

        if (issuesBuilder.Count > 0)
        {
            return new InterfaceMemberValidationResult(issues: issuesBuilder.ToImmutable());
        }

        return new InterfaceMemberValidationResult(context: new InterfaceContext(
            EventSymbols: eventSymbolsBuilder.ToImmutable(),
            PropertySymbols: propertySymbolsBuilder.ToImmutable(),
            MethodSymbols: methodSymbolsBuilder.ToImmutable()
        ));
    }
}
