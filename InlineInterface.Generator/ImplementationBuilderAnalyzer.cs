using System.Collections.Concurrent;
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
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArray.Create(
        InlineInterfaceDiagnostics.BuilderMustBeCompletedInSameExpressionRule,
        InlineInterfaceDiagnostics.MissingRequiredBuilderMembersRule
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationStartContext =>
        {
            var requiredMembersCache = new ConcurrentDictionary<
                INamedTypeSymbol,
                Lazy<RequiredBuilderMembersResult>
            >(SymbolEqualityComparer.Default);

            compilationStartContext.RegisterSyntaxNodeAction(
                context => AnalyzeInvocation(context, requiredMembersCache),
                SyntaxKind.InvocationExpression
            );
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<INamedTypeSymbol, Lazy<RequiredBuilderMembersResult>> requiredMembersCache
    )
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            !TargetTypeExtractor.IsCandidate(invocation) ||
            context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation ||
            !TryGetImplementationOfTarget(operation, invocation, out var target)
        )
        {
            return;
        }

        if (!TryCollectInvocationChain(
            invocation,
            out var chainInvocations,
            out var outermostExpression
        ))
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

        if (!TryCollectInvocationOperationChain(operation, chainInvocations, out var chainOperations) ||
            !IsBuildInvocation(chainOperations[^1], target.InterfaceSymbol)
        )
        {
            return;
        }

        var allowMissingImplementation = GetAllowMissingImplementation(operation);

        if (allowMissingImplementation is not false)
        {
            return;
        }

        var requiredMembersResult = GetRequiredBuilderMembersResult(requiredMembersCache, target);

        if (!requiredMembersResult.IsValid)
        {
            return;
        }

        var configuredMemberKeys = new HashSet<BuilderMemberKey>(BuilderMemberKeyComparer.Instance);

        for (var i = 1; i < chainInvocations.Length - 1; i++)
        {
            if (GetBuilderMemberKey(chainOperations[i].TargetMethod) is { } key)
            {
                configuredMemberKeys.Add(key);
            }
        }

        ImmutableArray<string>.Builder? missingMembersBuilder = null;

        foreach (var member in requiredMembersResult.Members)
        {
            if (configuredMemberKeys.Contains(member.Key))
            {
                continue;
            }

            missingMembersBuilder ??= ImmutableArray.CreateBuilder<string>();
            missingMembersBuilder.Add(CreateRequiredBuilderMemberDescription(member));
        }

        if (missingMembersBuilder is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.MissingRequiredBuilderMembersRule,
            location: lastInvocation.GetLocation(),
            messageArgs: [
                target.InterfaceSymbol.ToDisplayString(MinimallyQualifiedFormat),
                string.Join(", ", missingMembersBuilder),
            ]
        ));
    }

    private static RequiredBuilderMembersResult GetRequiredBuilderMembersResult(
        ConcurrentDictionary<INamedTypeSymbol, Lazy<RequiredBuilderMembersResult>> requiredMembersCache,
        ImplementationOfTarget target
    )
    {
        var typeSyntax = target.TypeSyntax;
        var lazyResult = requiredMembersCache.GetOrAdd(
            target.InterfaceSymbol,
            interfaceSymbol => new Lazy<RequiredBuilderMembersResult>(
                () => CreateRequiredBuilderMembersResult(interfaceSymbol, typeSyntax),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        return lazyResult.Value;
    }

    private static RequiredBuilderMembersResult CreateRequiredBuilderMembersResult(
        INamedTypeSymbol interfaceSymbol,
        TypeSyntax typeSyntax
    )
    {
        return InterfaceValidator.ValidateTargetInterface(interfaceSymbol, typeSyntax)
            is TargetInterfaceValidationResult.Success validationResult
                ? new RequiredBuilderMembersResult(
                    IsValid: true,
                    Members: GetRequiredBuilderMembers(validationResult.Contexts)
                )
                : new RequiredBuilderMembersResult(
                    IsValid: false,
                    Members: ImmutableArray<RequiredBuilderMember>.Empty
                );
    }

    private static ImmutableArray<RequiredBuilderMember> GetRequiredBuilderMembers(
        ImmutableArray<InterfaceContext> interfaceContexts
    )
    {
        var builder = ImmutableArray.CreateBuilder<RequiredBuilderMember>();
        var methodKeys = new HashSet<BuilderMemberKey>(BuilderMemberKeyComparer.Instance);
        var propertyMap = new Dictionary<PropertySignatureKey, PropertyRequirement>(
            PropertySignatureKeyComparer.Instance
        );

        foreach (var methodSymbol in interfaceContexts.SelectMany(static ctx => ctx.MethodSymbols))
        {
            if (methodSymbol.MethodKind
                is MethodKind.EventAdd or MethodKind.EventRemove
                or MethodKind.PropertyGet or MethodKind.PropertySet
            )
            {
                continue;
            }

            var builderMemberKey = CreateMethodBuilderMemberKey(methodSymbol);

            if (methodKeys.Add(builderMemberKey))
            {
                builder.Add(new RequiredBuilderMember(
                    Key: builderMemberKey,
                    Symbol: methodSymbol,
                    Kind: RequiredBuilderMemberKind.Method
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

        foreach (var requirement in propertyMap.Values)
        {
            var delegateSignatures = ImmutableArray.CreateBuilder<DelegateSignatureKey>();

            if (requirement.RequiresGetter)
            {
                delegateSignatures.Add(CreateDelegateSignature(
                    returnType: requirement.Symbol.Type,
                    parameters: requirement.Symbol.Parameters
                ));
            }

            if (requirement.RequiresSetter)
            {
                delegateSignatures.Add(CreateDelegateSignature(
                    returnType: null,
                    parameters: requirement.Symbol.Parameters,
                    trailingParameterType: requirement.Symbol.Type
                ));
            }

            builder.Add(new RequiredBuilderMember(
                Key: new BuilderMemberKey(
                    ApiName: requirement.Symbol.IsIndexer ? "Indexer" : requirement.Symbol.Name,
                    DelegateSignatures: delegateSignatures.ToImmutable()
                ),
                Symbol: requirement.Symbol,
                Kind: requirement.Symbol.IsIndexer
                    ? RequiredBuilderMemberKind.Indexer
                    : RequiredBuilderMemberKind.Property
            ));
        }

        return builder.ToImmutable();
    }

    private static BuilderMemberKey? GetBuilderMemberKey(IMethodSymbol methodSymbol)
    {
        var delegateSignatures = ImmutableArray.CreateBuilder<DelegateSignatureKey>();
        var parameterOffset = methodSymbol.IsExtensionMethod && methodSymbol.ReducedFrom is null ? 1 : 0;

        for (var i = parameterOffset; i < methodSymbol.Parameters.Length; i++)
        {
            var parameterSymbol = methodSymbol.Parameters[i];

            if (parameterSymbol.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod })
            {
                return null;
            }

            var delegateParameterOffset = HasEventDispatcherParameter(invokeMethod.Parameters) ? 1 : 0;

            delegateSignatures.Add(CreateDelegateSignature(
                returnType: invokeMethod.ReturnsVoid ? null : invokeMethod.ReturnType,
                parameters: invokeMethod.Parameters,
                parameterOffset: delegateParameterOffset
            ));
        }

        return new BuilderMemberKey(methodSymbol.Name, delegateSignatures.ToImmutable());
    }

    private static bool HasEventDispatcherParameter(ImmutableArray<IParameterSymbol> parameters)
    {
        return parameters.Length > 0 && parameters[0].Type is INamedTypeSymbol { Name: "EventDispatcher" };
    }

    private static BuilderMemberKey CreateMethodBuilderMemberKey(IMethodSymbol methodSymbol)
    {
        return new BuilderMemberKey(
            ApiName: methodSymbol.Name,
            DelegateSignatures: ImmutableArray.Create(CreateDelegateSignature(
                returnType: methodSymbol.ReturnsVoid ? null : methodSymbol.ReturnType,
                parameters: methodSymbol.Parameters
            ))
        );
    }

    private static PropertySignatureKey CreatePropertySignatureKey(IPropertySymbol propertySymbol)
    {
        return new PropertySignatureKey(
            ApiName: propertySymbol.IsIndexer ? "Indexer" : propertySymbol.Name,
            Type: propertySymbol.Type,
            ParameterTypes: GetParameterTypes(propertySymbol.Parameters)
        );
    }

    private static DelegateSignatureKey CreateDelegateSignature(
        ITypeSymbol? returnType,
        ImmutableArray<IParameterSymbol> parameters,
        int parameterOffset = 0,
        ITypeSymbol? trailingParameterType = null
    )
    {
        return new DelegateSignatureKey(
            ReturnType: returnType,
            ParameterTypes: GetParameterTypes(parameters, parameterOffset, trailingParameterType)
        );
    }

    private static ImmutableArray<ITypeSymbol> GetParameterTypes(
        ImmutableArray<IParameterSymbol> parameters,
        int parameterOffset = 0,
        ITypeSymbol? trailingParameterType = null
    )
    {
        var trailingParameterCount = trailingParameterType is null ? 0 : 1;
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(
            parameters.Length - parameterOffset + trailingParameterCount
        );

        for (var i = parameterOffset; i < parameters.Length; i++)
        {
            builder.Add(parameters[i].Type);
        }

        if (trailingParameterType is not null)
        {
            builder.Add(trailingParameterType);
        }

        return builder.ToImmutable();
    }

    private static string CreateRequiredBuilderMemberDescription(RequiredBuilderMember member)
    {
        return member switch
        {
            { Kind: RequiredBuilderMemberKind.Method, Symbol: IMethodSymbol methodSymbol } =>
                $"method '{CreateMethodDisplay(methodSymbol)}'",
            { Kind: RequiredBuilderMemberKind.Indexer, Symbol: IPropertySymbol propertySymbol } =>
                $"indexer '{CreateIndexerDisplay(propertySymbol)}'",
            { Kind: RequiredBuilderMemberKind.Property, Symbol: IPropertySymbol propertySymbol } =>
                $"property '{propertySymbol.Name}'",
            _ => throw new InvalidOperationException("Unexpected required builder member."),
        };
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

    private static bool IsBuildInvocation(
        IInvocationOperation invocation,
        INamedTypeSymbol targetInterfaceSymbol
    )
    {
        return invocation.TargetMethod.Name == "Build" &&
               SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ReturnType, targetInterfaceSymbol);
    }

    private static bool TryCollectInvocationChain(
        InvocationExpressionSyntax invocation,
        out ImmutableArray<InvocationExpressionSyntax> invocations,
        out ExpressionSyntax outermostExpression
    )
    {
        var builder = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        builder.Add(invocation);

        var current = invocation;

        while (true)
        {
            switch (current.Parent)
            {
                case MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == current:
                {
                    if (memberAccess.Parent is not InvocationExpressionSyntax parentInvocation ||
                        parentInvocation.Expression != memberAccess
                    )
                    {
                        invocations = ImmutableArray<InvocationExpressionSyntax>.Empty;
                        outermostExpression = memberAccess;

                        return false;
                    }

                    builder.Add(parentInvocation);
                    current = parentInvocation;

                    continue;
                }
                case InvocationExpressionSyntax parentInvocation when parentInvocation.Expression == current:
                {
                    invocations = ImmutableArray<InvocationExpressionSyntax>.Empty;
                    outermostExpression = parentInvocation;

                    return false;
                }
                default:
                {
                    invocations = builder.ToImmutable();
                    outermostExpression = current;

                    return true;
                }
            }
        }
    }

    private static bool TryCollectInvocationOperationChain(
        IInvocationOperation operation,
        ImmutableArray<InvocationExpressionSyntax> invocationSyntaxes,
        out ImmutableArray<IInvocationOperation> invocations
    )
    {
        if (invocationSyntaxes.Length == 0 || !HasSameSyntax(operation, invocationSyntaxes[0]))
        {
            invocations = ImmutableArray<IInvocationOperation>.Empty;

            return false;
        }

        var builder = ImmutableArray.CreateBuilder<IInvocationOperation>(invocationSyntaxes.Length);
        builder.Add(operation);

        IOperation current = operation;

        for (var i = 1; i < invocationSyntaxes.Length; i++)
        {
            var expectedSyntax = invocationSyntaxes[i];
            IOperation? parent = current.Parent;

            while (parent is not null &&
                   (parent is not IInvocationOperation || !HasSameSyntax(parent, expectedSyntax))
            )
            {
                parent = parent.Parent;
            }

            if (parent is not IInvocationOperation parentInvocation)
            {
                invocations = ImmutableArray<IInvocationOperation>.Empty;

                return false;
            }

            builder.Add(parentInvocation);
            current = parentInvocation;
        }

        invocations = builder.ToImmutable();

        return true;
    }

    private static bool HasSameSyntax(IOperation operation, SyntaxNode syntaxNode)
    {
        return operation.Syntax.SyntaxTree == syntaxNode.SyntaxTree && operation.Syntax.Span == syntaxNode.Span;
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

    private readonly record struct BuilderMemberKey(
        string ApiName,
        ImmutableArray<DelegateSignatureKey> DelegateSignatures
    );

    private readonly record struct DelegateSignatureKey(
        ITypeSymbol? ReturnType,
        ImmutableArray<ITypeSymbol> ParameterTypes
    );

    private readonly record struct PropertySignatureKey(
        string ApiName,
        ITypeSymbol Type,
        ImmutableArray<ITypeSymbol> ParameterTypes
    );

    private readonly record struct RequiredBuilderMember(
        BuilderMemberKey Key,
        ISymbol Symbol,
        RequiredBuilderMemberKind Kind
    );

    private readonly record struct RequiredBuilderMembersResult(
        bool IsValid,
        ImmutableArray<RequiredBuilderMember> Members
    );

    private readonly record struct PropertyRequirement(
        IPropertySymbol Symbol,
        bool RequiresGetter,
        bool RequiresSetter
    );

    private enum RequiredBuilderMemberKind
    {
        Method,
        Property,
        Indexer,
    }

    private sealed class BuilderMemberKeyComparer : IEqualityComparer<BuilderMemberKey>
    {
        public static BuilderMemberKeyComparer Instance { get; } = new();

        public bool Equals(BuilderMemberKey left, BuilderMemberKey right)
        {
            if (!StringComparer.Ordinal.Equals(left.ApiName, right.ApiName) ||
                left.DelegateSignatures.Length != right.DelegateSignatures.Length
            )
            {
                return false;
            }

            for (var i = 0; i < left.DelegateSignatures.Length; i++)
            {
                var leftSignature = left.DelegateSignatures[i];
                var rightSignature = right.DelegateSignatures[i];

                if (!SymbolEqualityComparer.Default.Equals(leftSignature.ReturnType, rightSignature.ReturnType) ||
                    !TypeArraysEqual(leftSignature.ParameterTypes, rightSignature.ParameterTypes)
                )
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(BuilderMemberKey key)
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(key.ApiName);
            hashCode = unchecked(hashCode * 31 + key.DelegateSignatures.Length);

            foreach (var signature in key.DelegateSignatures)
            {
                hashCode = AddTypeHashCode(hashCode, signature.ReturnType);
                hashCode = unchecked(hashCode * 31 + signature.ParameterTypes.Length);

                foreach (var parameterType in signature.ParameterTypes)
                {
                    hashCode = AddTypeHashCode(hashCode, parameterType);
                }
            }

            return hashCode;
        }
    }

    private sealed class PropertySignatureKeyComparer : IEqualityComparer<PropertySignatureKey>
    {
        public static PropertySignatureKeyComparer Instance { get; } = new();

        public bool Equals(PropertySignatureKey left, PropertySignatureKey right)
        {
            return StringComparer.Ordinal.Equals(left.ApiName, right.ApiName) &&
                   SymbolEqualityComparer.Default.Equals(left.Type, right.Type) &&
                   TypeArraysEqual(left.ParameterTypes, right.ParameterTypes);
        }

        public int GetHashCode(PropertySignatureKey key)
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(key.ApiName);
            hashCode = AddTypeHashCode(hashCode, key.Type);
            hashCode = unchecked(hashCode * 31 + key.ParameterTypes.Length);

            foreach (var parameterType in key.ParameterTypes)
            {
                hashCode = AddTypeHashCode(hashCode, parameterType);
            }

            return hashCode;
        }
    }

    private static bool TypeArraysEqual(
        ImmutableArray<ITypeSymbol> left,
        ImmutableArray<ITypeSymbol> right
    )
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int AddTypeHashCode(int hashCode, ITypeSymbol? typeSymbol)
    {
        return unchecked(hashCode * 31 + (typeSymbol is null
            ? 0
            : SymbolEqualityComparer.Default.GetHashCode(typeSymbol)));
    }
}
