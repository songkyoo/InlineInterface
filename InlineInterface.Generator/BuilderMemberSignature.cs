using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal readonly record struct BuilderMemberSignature(
    string ApiName,
    ImmutableArray<BuilderDelegateSignature> DelegateSignatures
);

internal readonly record struct BuilderDelegateSignature(
    ITypeSymbol? ReturnType,
    ImmutableArray<ITypeSymbol> ParameterTypes
);

internal static class BuilderMemberSignatureFactory
{
    public static BuilderMemberSignature Create(MethodSignature methodSignature)
    {
        return new BuilderMemberSignature(
            ApiName: methodSignature.Name,
            DelegateSignatures: ImmutableArray.Create(new BuilderDelegateSignature(
                ReturnType: methodSignature.ReturnType.SpecialType == SpecialType.System_Void
                    ? null
                    : methodSignature.ReturnType,
                ParameterTypes: GetParameterTypes(methodSignature.Parameters)
            ))
        );
    }

    public static BuilderMemberSignature Create(
        IPropertySymbol propertySymbol,
        bool requiresGetter,
        bool requiresSetter
    )
    {
        var delegateSignatures = ImmutableArray.CreateBuilder<BuilderDelegateSignature>(
            (requiresGetter ? 1 : 0) + (requiresSetter ? 1 : 0)
        );

        if (requiresGetter)
        {
            delegateSignatures.Add(CreateDelegateSignature(
                returnType: propertySymbol.Type,
                parameters: propertySymbol.Parameters
            ));
        }

        if (requiresSetter)
        {
            delegateSignatures.Add(CreateDelegateSignature(
                returnType: null,
                parameters: propertySymbol.Parameters,
                trailingParameterType: propertySymbol.Type
            ));
        }

        return new BuilderMemberSignature(
            ApiName: propertySymbol.IsIndexer ? "Indexer" : propertySymbol.Name,
            DelegateSignatures: delegateSignatures.ToImmutable()
        );
    }

    public static bool TryCreate(
        IMethodSymbol methodSymbol,
        out BuilderMemberSignature signature
    )
    {
        var delegateSignatures = ImmutableArray.CreateBuilder<BuilderDelegateSignature>();
        var parameterOffset = methodSymbol.IsExtensionMethod && methodSymbol.ReducedFrom is null ? 1 : 0;

        for (var i = parameterOffset; i < methodSymbol.Parameters.Length; i++)
        {
            if (methodSymbol.Parameters[i].Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod })
            {
                signature = default;

                return false;
            }

            var delegateParameterOffset = HasGeneratedEventDispatcherParameter(
                methodSymbol,
                invokeMethod.Parameters
            ) ? 1 : 0;

            delegateSignatures.Add(CreateDelegateSignature(
                returnType: invokeMethod.ReturnsVoid ? null : invokeMethod.ReturnType,
                parameters: invokeMethod.Parameters,
                parameterOffset: delegateParameterOffset
            ));
        }

        signature = new BuilderMemberSignature(
            ApiName: methodSymbol.Name,
            DelegateSignatures: delegateSignatures.ToImmutable()
        );

        return true;
    }

    private static bool HasGeneratedEventDispatcherParameter(
        IMethodSymbol builderMethod,
        ImmutableArray<IParameterSymbol> delegateParameters
    )
    {
        return delegateParameters.Length > 0 &&
               delegateParameters[0].Type is INamedTypeSymbol
               {
                   Name: "EventDispatcher",
                   ContainingType: { } containingBuilderType,
               } &&
               SymbolEqualityComparer.Default.Equals(containingBuilderType, builderMethod.ReturnType);
    }

    private static BuilderDelegateSignature CreateDelegateSignature(
        ITypeSymbol? returnType,
        ImmutableArray<IParameterSymbol> parameters,
        int parameterOffset = 0,
        ITypeSymbol? trailingParameterType = null
    )
    {
        return new BuilderDelegateSignature(
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
}

internal sealed class BuilderMemberSignatureComparer : IEqualityComparer<BuilderMemberSignature>
{
    public static BuilderMemberSignatureComparer Instance { get; } = new();

    public bool Equals(BuilderMemberSignature left, BuilderMemberSignature right)
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
                !InterfaceMemberSignatureHelpers.TypeSymbolsEqual(
                    leftSignature.ParameterTypes,
                    rightSignature.ParameterTypes
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(BuilderMemberSignature signature)
    {
        var hashCode = StringComparer.Ordinal.GetHashCode(signature.ApiName);
        hashCode = unchecked(hashCode * 31 + signature.DelegateSignatures.Length);

        foreach (var delegateSignature in signature.DelegateSignatures)
        {
            hashCode = InterfaceMemberSignatureHelpers.AddTypeHashCode(
                hashCode,
                delegateSignature.ReturnType
            );
            hashCode = unchecked(hashCode * 31 + delegateSignature.ParameterTypes.Length);

            foreach (var parameterType in delegateSignature.ParameterTypes)
            {
                hashCode = InterfaceMemberSignatureHelpers.AddTypeHashCode(hashCode, parameterType);
            }
        }

        return hashCode;
    }
}
