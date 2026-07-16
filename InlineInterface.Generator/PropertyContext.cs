namespace Macaron.InlineInterface;

public sealed class PropertyContext(PropertySignature signature, PropertyGenerationModel model)
{
    public PropertySignature Signature { get; } = signature;

    public PropertyGenerationModel Model { get; set; } = model;

    public int ModelIndex { get; set; } = -1;
}
