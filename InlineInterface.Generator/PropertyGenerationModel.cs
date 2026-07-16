namespace Macaron.InlineInterface;

public sealed record PropertyGenerationModel(
    bool IsIndexer,
    string Type,
    string Name,
    string ApiName,
    string Parameters,
    string Arguments,
    string? GetterDelegateType,
    string? SetterDelegateType,
    string? GetterName,
    string? SetterName,
    string? GetterParameterName,
    string? SetterParameterName,
    string? GetterFieldName,
    string? SetterFieldName,
    bool HasParameters
);
