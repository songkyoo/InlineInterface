using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed record MethodContext(
    ITypeSymbol ReturnTypeSymbol,
    ImmutableArray<IParameterSymbol> ParameterTypeSymbols,
    string ReturnType,
    string Parameters,
    string Arguments,
    string DelegateType,
    string Name,
    string UniqueName,
    string ParameterName,
    string FieldName
);
