namespace Macaron.InlineInterface;

public sealed record PropertyImplementationModel(
    int PropertyIndex,
    int InterfaceTypeIndex,
    bool HasGetter,
    bool HasSetter
);
