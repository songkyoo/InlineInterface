using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public static class ParameterStringHelpers
{
    public static (string Type, string Name) GetParameterString(
        IParameterSymbol parameterSymbol,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        bool includeModifier = false
    )
    {
        var typeString = SymbolHelpers.GetTypeString(parameterSymbol.Type, genericParameterMap);
        var nameString = GetCamelCaseName(parameterSymbol.Name);

        if (includeModifier && GetModifierString(parameterSymbol) is { Length: > 0 } modifierString)
        {
            typeString = $"{modifierString} {typeString}";
        }

        return (typeString, nameString);
    }

    public static string GetArgumentString(
        IParameterSymbol parameterSymbol,
        bool includeModifier = false
    )
    {
        var nameString = GetCamelCaseName(parameterSymbol.Name);

        if (includeModifier && GetModifierString(parameterSymbol) is { Length: > 0 } modifierString)
        {
            return $"{modifierString} {nameString}";
        }

        return nameString;
    }

    private static string GetCamelCaseName(string name)
    {
        return name.Length > 0 && char.IsLetter(name[0])
            ? char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name[1..] : "")
            : name;
    }

    private static string GetModifierString(IParameterSymbol parameterSymbol)
    {
        return parameterSymbol.RefKind switch
        {
            RefKind.Ref => "ref",
            RefKind.Out => "out",
            RefKind.In => "in",
            _ when parameterSymbol.IsParams => "params",
            _ => "",
        };
    }
}
