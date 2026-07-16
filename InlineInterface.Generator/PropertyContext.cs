using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal sealed class PropertyContext(
    ITypeSymbol typeSymbol,
    ImmutableArray<IParameterSymbol> parameterSymbols,
    PropertyGenerationModel model
)
{
    public ITypeSymbol TypeSymbol { get; } = typeSymbol;

    public ImmutableArray<IParameterSymbol> ParameterSymbols { get; } = parameterSymbols;

    public PropertyGenerationModel Model { get; set; } = model;

    public int ModelIndex { get; set; } = -1;
}
