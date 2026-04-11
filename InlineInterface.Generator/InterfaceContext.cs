using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed record InterfaceContext(
    INamedTypeSymbol TypeSymbol,
    ImmutableArray<IEventSymbol> EventSymbols,
    ImmutableArray<IPropertySymbol> PropertySymbols,
    ImmutableArray<IMethodSymbol> MethodSymbols
);
