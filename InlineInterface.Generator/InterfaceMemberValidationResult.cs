using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal readonly struct InterfaceMemberValidationResult
{
    private readonly int _index;
    private readonly InterfaceContext? _context;
    private readonly ImmutableArray<Diagnostic> _diagnostics;

    public InterfaceMemberValidationResult(InterfaceContext context)
    {
        _index = 1;
        _context = context;
        _diagnostics = ImmutableArray<Diagnostic>.Empty;
    }

    public InterfaceMemberValidationResult(ImmutableArray<Diagnostic> diagnostics)
    {
        _index = 2;
        _context = null;
        _diagnostics = diagnostics;
    }

    public bool TryGetContext([NotNullWhen(returnValue: true)] out InterfaceContext? context)
    {
        if (_index == 1)
        {
            context = _context!;

            return true;
        }

        context = null;

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
