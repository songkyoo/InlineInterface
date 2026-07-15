using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal sealed record EventContext(
    INamedTypeSymbol TypeSymbol,
    EventGenerationModel Model
);
