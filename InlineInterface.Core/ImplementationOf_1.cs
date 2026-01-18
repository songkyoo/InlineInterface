namespace Macaron.InlineInterface;

public readonly struct ImplementationOf<T>
{
    public T? Base { get; }

    internal ImplementationOf(T? @base) => Base = @base;
}
