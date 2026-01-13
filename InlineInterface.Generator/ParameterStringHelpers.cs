using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.InlineInterface;

internal static class ParameterStringHelpers
{
    public static (string Type, string Name) GetParameterString(IParameterSymbol parameterSymbol)
    {
        var typeString = parameterSymbol.Type.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
            IncludeNullableReferenceTypeModifier |
            UseSpecialTypes
        ));
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
