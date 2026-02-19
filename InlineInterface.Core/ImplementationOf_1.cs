namespace Macaron.InlineInterface;

public readonly struct ImplementationOf<T>
{
    public T? Base { get; }

    public bool AllowMissingImplementation { get; }

    internal ImplementationOf(T? @base, bool allowMissingImplementation)
    {
        Base = @base;
        AllowMissingImplementation = allowMissingImplementation;
    }
}
