namespace Macaron.InlineInterface;

internal sealed record PropertyImplementationModel(
    PropertyGenerationModel Property,
    string InterfaceType,
    bool HasGetter,
    bool HasSetter
);
