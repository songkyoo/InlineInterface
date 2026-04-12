using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed record EventContext(
    INamedTypeSymbol TypeSymbol,
    string Type,
    string Name,
    string UniqueName
);
