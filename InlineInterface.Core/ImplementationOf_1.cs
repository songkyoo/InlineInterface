namespace Macaron.InlineInterface;

public readonly struct ImplementationOf<T>
{
    public bool AllowMissingImplementation { get; }

    internal ImplementationOf(bool allowMissingImplementation)
    {
        AllowMissingImplementation = allowMissingImplementation;
    }
}
