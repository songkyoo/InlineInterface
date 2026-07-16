using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal sealed class EventContext(
    INamedTypeSymbol typeSymbol,
    EventGenerationModel model
)
{
    public INamedTypeSymbol TypeSymbol { get; } = typeSymbol;

    public EventGenerationModel Model { get; } = model;

    public int ModelIndex { get; set; } = -1;
}
