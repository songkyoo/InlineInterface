namespace Macaron.InlineInterface;

public readonly struct ImplementationOf<T>
    where T : notnull
{
    public bool AllowMissingImplementation { get; }

    internal ImplementationOf(bool allowMissingImplementation)
    {
        AllowMissingImplementation = allowMissingImplementation;
    }
}
