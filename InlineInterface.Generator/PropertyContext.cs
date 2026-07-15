using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal sealed record PropertyContext(
    ITypeSymbol TypeSymbol,
    ImmutableArray<IParameterSymbol> ParameterSymbols,
    PropertyGenerationModel Model,
    int ModelIndex = -1
);
