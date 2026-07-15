namespace Macaron.InlineInterface;

internal sealed record MethodGenerationModel(
    string ReturnType,
    string Parameters,
    string Arguments,
    string DelegateType,
    string Name,
    string UniqueName,
    string ParameterName,
    string FieldName
);
