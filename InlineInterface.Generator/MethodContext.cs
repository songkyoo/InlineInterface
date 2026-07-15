using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal sealed record MethodContext(
    ITypeSymbol ReturnTypeSymbol,
    ImmutableArray<IParameterSymbol> ParameterTypeSymbols,
    MethodGenerationModel Model,
    int ModelIndex = -1
);
