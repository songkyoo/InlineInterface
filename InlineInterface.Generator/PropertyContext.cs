using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed record PropertyContext(
    ITypeSymbol TypeSymbol,
    ImmutableArray<IParameterSymbol> ParameterSymbols,
    bool IsIndexer,
    string Type,
    string Name,
    string ApiName,
    string Parameters,
    string Arguments,
    bool RequiresGetter,
    bool RequiresSetter,
    string? GetterDelegateType,
    string? SetterDelegateType,
    string? GetterName,
    string? SetterName,
    string? GetterParameterName,
    string? SetterParameterName,
    string? GetterFieldName,
    string? SetterFieldName
);
