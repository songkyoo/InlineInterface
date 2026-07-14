using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImplementationBuilderAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        InlineInterfaceDiagnostics.BuilderMustBeCompletedInSameExpressionRule,
        InlineInterfaceDiagnostics.MissingRequiredBuilderMembersRule
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            !TargetTypeExtractor.IsCandidate(invocation) ||
            context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation ||
            !TryGetImplementationOfTarget(operation, invocation, out var target)
        )
        {
            return;
        }

        var outermostExpression = GetOutermostChainExpression(invocation);

        if (!TryCollectInvocationChain(outermostExpression, out var chainInvocations))
        {
            return;
        }

        var lastInvocation = chainInvocations[^1];

        if (!IsBuildInvocation(lastInvocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor: InlineInterfaceDiagnostics.BuilderMustBeCompletedInSameExpressionRule,
                location: outermostExpression.GetLocation()
            ));

            return;
        }

        var allowMissingImplementation = GetAllowMissingImplementation(operation);

        if (allowMissingImplementation is not false)
        {
            return;
        }

        if (InterfaceValidator.ValidateTargetInterface(target.InterfaceSymbol, target.TypeSyntax)
            is not TargetInterfaceValidationResult.Success validationResult
        )
        {
            return;
        }

        var configuredMemberKeys = chainInvocations
            .Skip(1)
            .Take(chainInvocations.Length - 2)
            .Select(builderInvocation => GetBuilderMemberKey(
                context.SemanticModel,
                builderInvocation,
                context.CancellationToken
            ))
            .Where(static key => key is not null)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var missingMembers = GetRequiredBuilderMembers(validationResult.Contexts)
            .Where(member => !configuredMemberKeys.Contains(member.Key))
            .Select(member => member.Description)
            .ToImmutableArray();

        if (missingMembers.Length == 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.MissingRequiredBuilderMembersRule,
            location: lastInvocation.GetLocation(),
            messageArgs: [
                target.InterfaceSymbol.ToDisplayString(MinimallyQualifiedFormat),
                string.Join(", ", missingMembers),
            ]
        ));
    }

    private static ImmutableArray<RequiredBuilderMember> GetRequiredBuilderMembers(
        ImmutableArray<InterfaceContext> interfaceContexts
    )
    {
        var methodMap = new Dictionary<string, RequiredBuilderMember>(StringComparer.Ordinal);
        var propertyMap = new Dictionary<string, PropertyRequirement>(StringComparer.Ordinal);

        foreach (var methodSymbol in interfaceContexts.SelectMany(static ctx => ctx.MethodSymbols))
        {
            if (methodSymbol.MethodKind
                is MethodKind.EventAdd or MethodKind.EventRemove
                or MethodKind.PropertyGet or MethodKind.PropertySet
            )
            {
                continue;
            }

            var signatureKey = CreateMethodSignatureKey(methodSymbol);

            if (!methodMap.ContainsKey(signatureKey))
            {
                methodMap.Add(signatureKey, new RequiredBuilderMember(
                    Key: CreateBuilderMemberKey(
                        apiName: methodSymbol.Name,
                        delegateSignatures: ImmutableArray.Create(CreateDelegateSignature(
                            returnType: methodSymbol.ReturnType,
                            parameterTypes: methodSymbol.Parameters.Select(static parameter => parameter.Type)
                        ))
                    ),
                    Description: $"method '{CreateMethodDisplay(methodSymbol)}'"
                ));
            }
        }

        foreach (var propertySymbol in interfaceContexts.SelectMany(static ctx => ctx.PropertySymbols))
        {
            var propertySignatureKey = CreatePropertySignatureKey(propertySymbol);

            if (!propertyMap.TryGetValue(propertySignatureKey, out var existing))
            {
                propertyMap.Add(propertySignatureKey, new PropertyRequirement(
                    Symbol: propertySymbol,
                    RequiresGetter: propertySymbol.GetMethod != null,
                    RequiresSetter: propertySymbol.SetMethod != null
                ));

                continue;
            }

            propertyMap[propertySignatureKey] = existing with
            {
                RequiresGetter = existing.RequiresGetter || propertySymbol.GetMethod != null,
                RequiresSetter = existing.RequiresSetter || propertySymbol.SetMethod != null,
            };
        }

        var builder = ImmutableArray.CreateBuilder<RequiredBuilderMember>(methodMap.Count + propertyMap.Count);

        builder.AddRange(methodMap.Values);

        foreach (var requirement in propertyMap.Values)
        {
            var delegateSignatures = ImmutableArray.CreateBuilder<string>();

            if (requirement.RequiresGetter)
            {
                delegateSignatures.Add(CreateDelegateSignature(
                    returnType: requirement.Symbol.Type,
                    parameterTypes: requirement.Symbol.Parameters.Select(static parameter => parameter.Type)
                ));
            }

            if (requirement.RequiresSetter)
            {
                delegateSignatures.Add(CreateDelegateSignature(
                    returnType: null,
                    parameterTypes: requirement.Symbol.Parameters
                        .Select(static parameter => parameter.Type)
                        .Concat([requirement.Symbol.Type])
                ));
            }

            builder.Add(new RequiredBuilderMember(
                Key: CreateBuilderMemberKey(
                    apiName: requirement.Symbol.IsIndexer ? "Indexer" : requirement.Symbol.Name,
                    delegateSignatures: delegateSignatures.ToImmutable()
                ),
                Description: requirement.Symbol.IsIndexer
                    ? $"indexer '{CreateIndexerDisplay(requirement.Symbol)}'"
                    : $"property '{requirement.Symbol.Name}'"
            ));
        }

        return builder.ToImmutable();
    }

    private static string? GetBuilderMemberKey(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken
    )
    {
        if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        var delegateSignatures = ImmutableArray.CreateBuilder<string>();

        foreach (var parameterSymbol in methodSymbol.Parameters)
        {
            if (parameterSymbol.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod })
            {
                return null;
            }

            delegateSignatures.Add(CreateDelegateSignature(
                returnType: invokeMethod.ReturnsVoid ? null : invokeMethod.ReturnType,
                parameterTypes: TrimEventDispatcherParameter(invokeMethod.Parameters)
                    .Select(static parameter => parameter.Type)
            ));
        }

        return CreateBuilderMemberKey(methodSymbol.Name, delegateSignatures.ToImmutable());
    }

    private static ImmutableArray<IParameterSymbol> TrimEventDispatcherParameter(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0 || parameters[0].Type is not INamedTypeSymbol { Name: "EventDispatcher" })
        {
            return parameters;
        }

        return parameters.RemoveAt(0);
    }

    private static string CreateBuilderMemberKey(string apiName, ImmutableArray<string> delegateSignatures)
    {
        return $"{apiName}|{string.Join("|", delegateSignatures)}";
    }

    private static string CreateMethodSignatureKey(IMethodSymbol methodSymbol)
    {
        return $"{methodSymbol.Name}|{CreateTypeKey(methodSymbol.ReturnType)}|{string.Join("|", methodSymbol.Parameters.Select(static parameter => CreateTypeKey(parameter.Type)))}";
    }

    private static string CreatePropertySignatureKey(IPropertySymbol propertySymbol)
    {
        var apiName = propertySymbol.IsIndexer ? "Indexer" : propertySymbol.Name;
        var typeKey = CreateTypeKey(propertySymbol.Type);
        var parameterKeys = string.Join("|", propertySymbol.Parameters.Select(static parameter => CreateTypeKey(parameter.Type)));

        return $"{apiName}|{typeKey}|{parameterKeys}";
    }

    private static string CreateDelegateSignature(ITypeSymbol? returnType, IEnumerable<ITypeSymbol> parameterTypes)
    {
        var parameterKeys = string.Join(", ", parameterTypes.Select(CreateTypeKey));
        var returnKey = returnType is null ? "void" : CreateTypeKey(returnType);

        return $"({parameterKeys})->{returnKey}";
    }

    private static string CreateTypeKey(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedFormat);
    }

    private static string CreateMethodDisplay(IMethodSymbol methodSymbol)
    {
        var parameters = string.Join(", ", methodSymbol.Parameters.Select(
            parameter => $"{parameter.Type.ToDisplayString(MinimallyQualifiedFormat)} {parameter.Name}"
        ));

        return $"{methodSymbol.Name}({parameters})";
    }

    private static string CreateIndexerDisplay(IPropertySymbol propertySymbol)
    {
        var parameters = string.Join(", ", propertySymbol.Parameters.Select(
            parameter => $"{parameter.Type.ToDisplayString(MinimallyQualifiedFormat)} {parameter.Name}"
        ));

        return $"this[{parameters}]";
    }

    private static bool IsBuildInvocation(InvocationExpressionSyntax invocation)
    {
        return GetInvokedMethodName(invocation) == "Build";
    }

    private static ExpressionSyntax GetOutermostChainExpression(InvocationExpressionSyntax invocation)
    {
        ExpressionSyntax current = invocation;

        while (true)
        {
            switch (current.Parent)
            {
                case MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == current:
                {
                    current = memberAccess;

                    continue;
                }
                case InvocationExpressionSyntax parentInvocation when parentInvocation.Expression == current:
                {
                    current = parentInvocation;

                    continue;
                }
                default:
                    return current;
            }
        }
    }

    private static bool TryCollectInvocationChain(
        ExpressionSyntax expression,
        out ImmutableArray<InvocationExpressionSyntax> invocations
    )
    {
        var builder = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        var success = TryCollectInvocationChainCore(expression, builder);

        invocations = success ? builder.ToImmutable() : ImmutableArray<InvocationExpressionSyntax>.Empty;

        return success;
    }

    private static bool TryCollectInvocationChainCore(
        ExpressionSyntax expression,
        ImmutableArray<InvocationExpressionSyntax>.Builder builder
    )
    {
        if (expression is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        switch (invocation.Expression)
        {
            case MemberAccessExpressionSyntax memberAccess:
            {
                if (memberAccess.Expression is InvocationExpressionSyntax innerInvocation &&
                    !TryCollectInvocationChainCore(innerInvocation, builder)
                )
                {
                    return false;
                }

                builder.Add(invocation);

                return true;
            }
            case SimpleNameSyntax:
            {
                builder.Add(invocation);

                return true;
            }
            default:
            {
                return false;
            }
        }
    }

    private static bool TryGetImplementationOfTarget(
        IInvocationOperation operation,
        InvocationExpressionSyntax invocation,
        out ImplementationOfTarget target
    )
    {
        target = default;

        if (TargetTypeExtractor.GetCandidateGenericName(invocation) is not { } genericNameSyntax ||
            operation.TargetMethod is not { IsStatic: true, Name: "Of" } methodSymbol ||
            !TargetTypeExtractor.IsImplementationType(methodSymbol.ContainingType) ||
            genericNameSyntax.TypeArgumentList.Arguments is not [{ } typeArgumentSyntax] ||
            methodSymbol.TypeArguments is not [{ } typeArgument] ||
            typeArgument is not INamedTypeSymbol interfaceSymbol
        )
        {
            return false;
        }

        target = new ImplementationOfTarget(interfaceSymbol, typeArgumentSyntax);

        return true;
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: { } name } => name.Identifier.ValueText,
            SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };
    }

    private static bool? GetAllowMissingImplementation(IInvocationOperation operation)
    {
        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter?.Name != "allowMissingImplementation")
            {
                continue;
            }

            return argument.Value.ConstantValue is { HasValue: true, Value: bool value }
                ? value
                : null;
        }

        return false;
    }

    private readonly record struct ImplementationOfTarget(
        INamedTypeSymbol InterfaceSymbol,
        TypeSyntax TypeSyntax
    );

    private readonly record struct RequiredBuilderMember(
        string Key,
        string Description
    );

    private readonly record struct PropertyRequirement(
        IPropertySymbol Symbol,
        bool RequiresGetter,
        bool RequiresSetter
    );
}
