namespace Macaron.InlineInterface;

public static class Implementation
{
    public static ImplementationOf<T> Of<T>(
        bool allowMissingImplementation = false
    )
    {
        return new ImplementationOf<T>(allowMissingImplementation);
    }
}
