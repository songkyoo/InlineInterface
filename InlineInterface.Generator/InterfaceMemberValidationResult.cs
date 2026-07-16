using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Macaron.InlineInterface;

internal readonly struct InterfaceMemberValidationResult
{
    private readonly int _index;
    private readonly InterfaceContext? _context;
    private readonly ImmutableArray<InterfaceValidationIssue> _issues;

    public InterfaceMemberValidationResult(InterfaceContext context)
    {
        _index = 1;
        _context = context;
        _issues = ImmutableArray<InterfaceValidationIssue>.Empty;
    }

    public InterfaceMemberValidationResult(ImmutableArray<InterfaceValidationIssue> issues)
    {
        _index = 2;
        _context = null;
        _issues = issues;
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

    public bool TryGetIssues(out ImmutableArray<InterfaceValidationIssue> issues)
    {
        if (_index == 2)
        {
            issues = _issues;

            return true;
        }

        issues = default;

        return false;
    }
}
