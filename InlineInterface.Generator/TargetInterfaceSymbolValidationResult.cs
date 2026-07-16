using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal abstract record TargetInterfaceSymbolValidationResult
{
    public sealed record Success(
        INamedTypeSymbol InterfaceSymbol,
        ImmutableArray<InterfaceContext> Contexts
    ) : TargetInterfaceSymbolValidationResult;

    public sealed record Failure(
        ImmutableArray<InterfaceValidationIssue> Issues
    ) : TargetInterfaceSymbolValidationResult;
}
