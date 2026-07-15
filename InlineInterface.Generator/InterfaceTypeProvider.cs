using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal sealed class InterfaceTypeProvider(ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap)
{
    #region Fields
    private readonly Dictionary<INamedTypeSymbol, int> _cache = new(SymbolEqualityComparer.Default);
    private readonly List<string> _types = [];
    #endregion

    #region Methods
    public int GetIndex(INamedTypeSymbol interfaceSymbol)
    {
        if (_cache.TryGetValue(interfaceSymbol, out var index))
        {
            return index;
        }

        index = _types.Count;

        _cache.Add(interfaceSymbol, index);
        _types.Add(SymbolHelpers.GetTypeString(interfaceSymbol, genericParameterMap));

        return index;
    }

    public ImmutableArray<string> ToImmutableArray()
    {
        return _types.ToImmutableArray();
    }
    #endregion
}
