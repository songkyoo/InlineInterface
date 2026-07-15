namespace Macaron.InlineInterface;

internal sealed record MethodImplementationModel(
    MethodGenerationModel Method,
    string InterfaceType
);
