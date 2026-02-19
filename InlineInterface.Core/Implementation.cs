namespace Macaron.InlineInterface;

public static class Implementation
{
    public static ImplementationOf<T> Of<T>(
        T? @base = default,
        bool allowMissingImplementation = false
    )
    {
        return new ImplementationOf<T>(@base, allowMissingImplementation);
    }
}
