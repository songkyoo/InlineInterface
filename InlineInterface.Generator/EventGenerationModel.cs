namespace Macaron.InlineInterface;

public sealed record EventGenerationModel(
    string Type,
    string Name,
    string UniqueName,
    string DispatcherParameters,
    string DispatcherArguments,
    bool ReturnsVoid
);
