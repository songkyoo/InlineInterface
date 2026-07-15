namespace Macaron.InlineInterface;

internal sealed record PropertyImplementationModel(
    int PropertyIndex,
    int InterfaceTypeIndex,
    bool HasGetter,
    bool HasSetter
);
