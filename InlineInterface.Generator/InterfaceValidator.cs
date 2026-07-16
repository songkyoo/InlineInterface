using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface;

public static class InterfaceValidator
{
    public static TargetInterfaceValidationResult ValidateTargetInterface(
        INamedTypeSymbol interfaceSymbol,
        TypeSyntax typeSyntax,
        CancellationToken cancellationToken = default
    )
    {
        var result = ValidateTargetInterface(interfaceSymbol, cancellationToken);

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
            cancellationToken.ThrowIfCancellationRequested();
            diagnosticsBuilder.Add(issue.CreateDiagnostic(typeSyntax));
        }

        return new TargetInterfaceValidationResult.Failure(diagnosticsBuilder.ToImmutable());
    }

    public static TargetInterfaceSymbolValidationResult ValidateTargetInterface(
        INamedTypeSymbol interfaceSymbol,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var interfaceContextsBuilder = ImmutableArray.CreateBuilder<InterfaceContext>();
        var issuesBuilder = ImmutableArray.CreateBuilder<InterfaceValidationIssue>();

        AddValidationResult(
            ValidateInterfaceMembers(interfaceSymbol, cancellationToken),
            interfaceContextsBuilder,
            issuesBuilder
        );

        foreach (var inheritedInterfaceSymbol in interfaceSymbol.AllInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddValidationResult(
                ValidateInterfaceMembers(inheritedInterfaceSymbol, cancellationToken),
                interfaceContextsBuilder,
                issuesBuilder
            );
        }

        return issuesBuilder.Count > 0
            ? new TargetInterfaceSymbolValidationResult.Failure(Issues: issuesBuilder.ToImmutable())
            : new TargetInterfaceSymbolValidationResult.Success(
                InterfaceSymbol: interfaceSymbol,
                Contexts: interfaceContextsBuilder.ToImmutable()
            );
    }

    private static void AddValidationResult(
        InterfaceMemberValidationResult result,
        ImmutableArray<InterfaceContext>.Builder interfaceContextsBuilder,
        ImmutableArray<InterfaceValidationIssue>.Builder issuesBuilder
    )
    {
        if (result.TryGetContext(out var interfaceContext))
        {
            interfaceContextsBuilder.Add(interfaceContext);
        }
        else if (result.TryGetIssues(out var issues))
        {
            issuesBuilder.AddRange(issues);
        }
    }

    private static InterfaceMemberValidationResult ValidateInterfaceMembers(
        INamedTypeSymbol interfaceSymbol,
        CancellationToken cancellationToken
    )
    {
        var eventSymbolsBuilder = ImmutableArray.CreateBuilder<IEventSymbol>();
        var propertySymbolsBuilder = ImmutableArray.CreateBuilder<IPropertySymbol>();
        var methodSymbolsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
        var issuesBuilder = ImmutableArray.CreateBuilder<InterfaceValidationIssue>();

        foreach (var member in interfaceSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                        && HasUnsupportedEventParameter(invokeMethod.Parameters, cancellationToken)
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

                    if (HasUnsupportedMethodParameter(method.Parameters, cancellationToken))
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

    private static bool HasUnsupportedEventParameter(
        ImmutableArray<IParameterSymbol> parameters,
        CancellationToken cancellationToken
    )
    {
        foreach (var parameter in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameter.RefKind is not RefKind.None and not RefKind.In || parameter.IsParams)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnsupportedMethodParameter(
        ImmutableArray<IParameterSymbol> parameters,
        CancellationToken cancellationToken
    )
    {
        foreach (var parameter in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (parameter.RefKind != RefKind.None || parameter.IsParams)
            {
                return true;
            }
        }

        return false;
    }
}
