using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal readonly struct BuilderMemberSignature
{
    public BuilderMemberSignature(
        string apiName,
        BuilderDelegateSignature firstDelegate
    )
    {
        ApiName = apiName;
        DelegateCount = 1;
        FirstDelegate = firstDelegate;
        SecondDelegate = default;
    }

    public BuilderMemberSignature(
        string apiName,
        BuilderDelegateSignature firstDelegate,
        BuilderDelegateSignature secondDelegate
    )
    {
        ApiName = apiName;
        DelegateCount = 2;
        FirstDelegate = firstDelegate;
        SecondDelegate = secondDelegate;
    }

    public string ApiName { get; }

    public int DelegateCount { get; }

    public BuilderDelegateSignature FirstDelegate { get; }

    public BuilderDelegateSignature SecondDelegate { get; }
}

internal readonly struct BuilderDelegateSignature
{
    public BuilderDelegateSignature(
        ITypeSymbol? returnType,
        ImmutableArray<IParameterSymbol> parameters,
        int parameterOffset = 0,
        ITypeSymbol? trailingParameterType = null
    )
    {
        ReturnType = returnType;
        Parameters = parameters;
        ParameterOffset = parameterOffset;
        TrailingParameterType = trailingParameterType;
    }

    public ITypeSymbol? ReturnType { get; }

    public ImmutableArray<IParameterSymbol> Parameters { get; }

    public int ParameterOffset { get; }

    public ITypeSymbol? TrailingParameterType { get; }

    public int ParameterCount =>
        Parameters.Length - ParameterOffset + (TrailingParameterType is null ? 0 : 1);

    public ITypeSymbol GetParameterType(int index)
    {
        if ((uint)index >= (uint)ParameterCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var parameterIndex = index + ParameterOffset;

        return parameterIndex < Parameters.Length
            ? Parameters[parameterIndex].Type
            : TrailingParameterType!;
    }
}

internal static class BuilderMemberSignatureFactory
{
    public static BuilderMemberSignature Create(MethodSignature methodSignature)
    {
        return new BuilderMemberSignature(
            apiName: methodSignature.Name,
            firstDelegate: new BuilderDelegateSignature(
                returnType: methodSignature.ReturnType.SpecialType == SpecialType.System_Void
                    ? null
                    : methodSignature.ReturnType,
                parameters: methodSignature.Parameters
            )
        );
    }

    public static BuilderMemberSignature Create(
        IPropertySymbol propertySymbol,
        bool requiresGetter,
        bool requiresSetter
    )
    {
        var apiName = propertySymbol.IsIndexer ? "Indexer" : propertySymbol.Name;

        if (requiresGetter)
        {
            var getterSignature = new BuilderDelegateSignature(
                returnType: propertySymbol.Type,
                parameters: propertySymbol.Parameters
            );

            if (requiresSetter)
            {
                return new BuilderMemberSignature(
                    apiName,
                    getterSignature,
                    new BuilderDelegateSignature(
                        returnType: null,
                        parameters: propertySymbol.Parameters,
                        trailingParameterType: propertySymbol.Type
                    )
                );
            }

            return new BuilderMemberSignature(apiName, getterSignature);
        }

        if (requiresSetter)
        {
            return new BuilderMemberSignature(
                apiName,
                new BuilderDelegateSignature(
                    returnType: null,
                    parameters: propertySymbol.Parameters,
                    trailingParameterType: propertySymbol.Type
                )
            );
        }

        throw new InvalidOperationException("A required property must have at least one accessor.");
    }

    public static bool TryCreate(
        IMethodSymbol methodSymbol,
        out BuilderMemberSignature signature
    )
    {
        var parameterOffset = methodSymbol.IsExtensionMethod && methodSymbol.ReducedFrom is null ? 1 : 0;
        var delegateCount = methodSymbol.Parameters.Length - parameterOffset;

        if (delegateCount is < 1 or > 2 ||
            !TryCreateDelegateSignature(
                methodSymbol,
                methodSymbol.Parameters[parameterOffset],
                out var firstDelegate
            )
        )
        {
            signature = default;

            return false;
        }

        if (delegateCount == 1)
        {
            signature = new BuilderMemberSignature(methodSymbol.Name, firstDelegate);

            return true;
        }

        if (!TryCreateDelegateSignature(
            methodSymbol,
            methodSymbol.Parameters[parameterOffset + 1],
            out var secondDelegate
        ))
        {
            signature = default;

            return false;
        }

        signature = new BuilderMemberSignature(
            methodSymbol.Name,
            firstDelegate,
            secondDelegate
        );

        return true;
    }

    private static bool TryCreateDelegateSignature(
        IMethodSymbol builderMethod,
        IParameterSymbol parameterSymbol,
        out BuilderDelegateSignature signature
    )
    {
        if (parameterSymbol.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod })
        {
            signature = default;

            return false;
        }

        signature = new BuilderDelegateSignature(
            returnType: invokeMethod.ReturnsVoid ? null : invokeMethod.ReturnType,
            parameters: invokeMethod.Parameters,
            parameterOffset: HasGeneratedEventDispatcherParameter(
                builderMethod,
                invokeMethod.Parameters
            ) ? 1 : 0
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
}

internal sealed class BuilderMemberSignatureComparer : IEqualityComparer<BuilderMemberSignature>
{
    public static BuilderMemberSignatureComparer Instance { get; } = new();

    public bool Equals(BuilderMemberSignature left, BuilderMemberSignature right)
    {
        return StringComparer.Ordinal.Equals(left.ApiName, right.ApiName) &&
               left.DelegateCount == right.DelegateCount &&
               DelegateSignaturesEqual(left.FirstDelegate, right.FirstDelegate) &&
               (left.DelegateCount == 1 ||
                DelegateSignaturesEqual(left.SecondDelegate, right.SecondDelegate));
    }

    public int GetHashCode(BuilderMemberSignature signature)
    {
        var hashCode = StringComparer.Ordinal.GetHashCode(signature.ApiName);
        hashCode = unchecked(hashCode * 31 + signature.DelegateCount);
        hashCode = AddDelegateSignatureHashCode(hashCode, signature.FirstDelegate);

        return signature.DelegateCount == 1
            ? hashCode
            : AddDelegateSignatureHashCode(hashCode, signature.SecondDelegate);
    }

    private static bool DelegateSignaturesEqual(
        BuilderDelegateSignature left,
        BuilderDelegateSignature right
    )
    {
        if (!SymbolEqualityComparer.Default.Equals(left.ReturnType, right.ReturnType) ||
            left.ParameterCount != right.ParameterCount
        )
        {
            return false;
        }

        for (var i = 0; i < left.ParameterCount; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                left.GetParameterType(i),
                right.GetParameterType(i)
            ))
            {
                return false;
            }
        }

        return true;
    }

    private static int AddDelegateSignatureHashCode(
        int hashCode,
        BuilderDelegateSignature signature
    )
    {
        hashCode = InterfaceMemberSignatureHelpers.AddTypeHashCode(hashCode, signature.ReturnType);
        hashCode = unchecked(hashCode * 31 + signature.ParameterCount);

        for (var i = 0; i < signature.ParameterCount; i++)
        {
            hashCode = InterfaceMemberSignatureHelpers.AddTypeHashCode(
                hashCode,
                signature.GetParameterType(i)
            );
        }

        return hashCode;
    }
}
