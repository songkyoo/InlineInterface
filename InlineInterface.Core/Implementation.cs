namespace Macaron.InlineInterface;

public static class Implementation
{
    public static ImplementationOf<T> Of<T>(T? @base = default) => new(@base);
}
