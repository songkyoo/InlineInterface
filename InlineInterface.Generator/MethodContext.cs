namespace Macaron.InlineInterface;

public sealed class MethodContext(MethodSignature signature, MethodGenerationModel model)
{
    public MethodSignature Signature { get; } = signature;

    public MethodGenerationModel Model { get; } = model;

    public int ModelIndex { get; set; } = -1;
}
