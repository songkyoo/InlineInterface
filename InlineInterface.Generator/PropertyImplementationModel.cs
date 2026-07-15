namespace Macaron.InlineInterface;

internal sealed record PropertyImplementationModel(
    int PropertyIndex,
    string InterfaceType,
    bool HasGetter,
    bool HasSetter
);
