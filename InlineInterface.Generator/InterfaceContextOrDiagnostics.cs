using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal readonly struct InterfaceContextOrDiagnostics
{
    private readonly int _index;
    private readonly InterfaceContext? _interfaceContext;
    private readonly ImmutableArray<Diagnostic> _diagnostics;

    public InterfaceContextOrDiagnostics(InterfaceContext interfaceContext)
    {
        _index = 1;
        _interfaceContext = interfaceContext;
        _diagnostics = ImmutableArray<Diagnostic>.Empty;
    }

    public InterfaceContextOrDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        _index = 2;
        _interfaceContext = null;
        _diagnostics = diagnostics;
    }

    public bool TryGetInterfaceContext([NotNullWhen(returnValue: true)] out InterfaceContext? value)
    {
        if (_index == 1)
        {
            value = _interfaceContext!;

            return true;
        }

        value = null;

        return false;
    }

    public bool TryGetDiagnostics(out ImmutableArray<Diagnostic> diagnostics)
    {
        if (_index == 2)
        {
            diagnostics = _diagnostics;

            return true;
        }

        diagnostics = default;

        return false;
    }
}
