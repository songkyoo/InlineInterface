using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal readonly record struct MethodSignature(
    string Name,
    ITypeSymbol ReturnType,
    ImmutableArray<IParameterSymbol> Parameters
)
{
    public static MethodSignature Create(IMethodSymbol methodSymbol)
    {
        return new MethodSignature(
            Name: methodSymbol.Name,
            ReturnType: methodSymbol.ReturnType,
            Parameters: methodSymbol.Parameters
        );
    }
}

internal readonly record struct PropertySignature(
    string Name,
    ITypeSymbol Type,
    ImmutableArray<IParameterSymbol> Parameters
)
{
    public static PropertySignature Create(IPropertySymbol propertySymbol)
    {
        return new PropertySignature(
            Name: propertySymbol.Name,
            Type: propertySymbol.Type,
            Parameters: propertySymbol.Parameters
        );
    }
}

internal sealed class MethodSignatureComparer : IEqualityComparer<MethodSignature>
{
    public static MethodSignatureComparer Instance { get; } = new();

    public bool Equals(MethodSignature left, MethodSignature right)
    {
        return StringComparer.Ordinal.Equals(left.Name, right.Name) &&
               SymbolEqualityComparer.Default.Equals(left.ReturnType, right.ReturnType) &&
               InterfaceMemberSignatureHelpers.ParametersEqual(left.Parameters, right.Parameters);
    }

    public int GetHashCode(MethodSignature signature)
    {
        var hashCode = StringComparer.Ordinal.GetHashCode(signature.Name);
        hashCode = InterfaceMemberSignatureHelpers.AddTypeHashCode(hashCode, signature.ReturnType);

        return InterfaceMemberSignatureHelpers.AddParametersHashCode(hashCode, signature.Parameters);
    }
}

internal sealed class PropertySignatureComparer : IEqualityComparer<PropertySignature>
{
    public static PropertySignatureComparer Instance { get; } = new();

    public bool Equals(PropertySignature left, PropertySignature right)
    {
        return StringComparer.Ordinal.Equals(left.Name, right.Name) &&
               SymbolEqualityComparer.Default.Equals(left.Type, right.Type) &&
               InterfaceMemberSignatureHelpers.ParametersEqual(left.Parameters, right.Parameters);
    }

    public int GetHashCode(PropertySignature signature)
    {
        var hashCode = StringComparer.Ordinal.GetHashCode(signature.Name);
        hashCode = InterfaceMemberSignatureHelpers.AddTypeHashCode(hashCode, signature.Type);

        return InterfaceMemberSignatureHelpers.AddParametersHashCode(hashCode, signature.Parameters);
    }
}

internal static class InterfaceMemberSignatureHelpers
{
    public static bool ParametersEqual(
        ImmutableArray<IParameterSymbol> left,
        ImmutableArray<IParameterSymbol> right
    )
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[i].Type, right[i].Type) ||
                left[i].RefKind != right[i].RefKind ||
                left[i].IsParams != right[i].IsParams
            )
            {
                return false;
            }
        }

        return true;
    }

    public static int AddParametersHashCode(
        int hashCode,
        ImmutableArray<IParameterSymbol> parameters
    )
    {
        hashCode = unchecked(hashCode * 31 + parameters.Length);

        foreach (var parameter in parameters)
        {
            hashCode = AddTypeHashCode(hashCode, parameter.Type);
            hashCode = unchecked(hashCode * 31 + (int)parameter.RefKind);
            hashCode = unchecked(hashCode * 31 + (parameter.IsParams ? 1 : 0));
        }

        return hashCode;
    }

    public static int AddTypeHashCode(int hashCode, ITypeSymbol? typeSymbol)
    {
        return unchecked(hashCode * 31 + (typeSymbol is null
            ? 0
            : SymbolEqualityComparer.Default.GetHashCode(typeSymbol)));
    }
}
