using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal static class ParameterStringHelpers
{
    public static (string Type, string Name) GetParameterString(
        IParameterSymbol parameterSymbol,
        ImmutableDictionary<ITypeParameterSymbol, string > genericParameterMap
    )
    {
        var typeString = SymbolHelpers.GetTypeString(parameterSymbol.Type, genericParameterMap);
        var nameString = GetCamelCaseName(parameterSymbol.Name);

        return (typeString, nameString);
    }

    private static string GetCamelCaseName(string name)
    {
        return name.Length > 0 && char.IsLetter(name[0])
            ? char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name[1..] : "")
            : name;
    }
}
