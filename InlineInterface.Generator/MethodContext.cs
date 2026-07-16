using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal sealed class MethodContext(
    ITypeSymbol returnTypeSymbol,
    ImmutableArray<IParameterSymbol> parameterTypeSymbols,
    MethodGenerationModel model
)
{
    public ITypeSymbol ReturnTypeSymbol { get; } = returnTypeSymbol;

    public ImmutableArray<IParameterSymbol> ParameterTypeSymbols { get; } = parameterTypeSymbols;

    public MethodGenerationModel Model { get; } = model;

    public int ModelIndex { get; set; } = -1;
}
