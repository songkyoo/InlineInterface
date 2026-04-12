using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed class InterfaceTypeStringProvider(ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap)
{
    #region Fields
    private readonly Dictionary<INamedTypeSymbol, string> _cache = new(SymbolEqualityComparer.Default);
    #endregion

    #region Methods
    public string GetInterfaceTypeName(INamedTypeSymbol interfaceSymbol)
    {
        if (_cache.TryGetValue(interfaceSymbol, out var name))
        {
            return name;
        }

        name = SymbolHelpers.GetTypeString(interfaceSymbol, genericParameterMap);

        _cache.Add(interfaceSymbol, name);

        return name;
    }
    #endregion
}
