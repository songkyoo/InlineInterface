using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal abstract record TargetInterfaceValidationResult
{
    public sealed record Success(
        INamedTypeSymbol InterfaceSymbol,
        ImmutableArray<InterfaceContext> Contexts
    ) : TargetInterfaceValidationResult;

    public sealed record Failure(
        ImmutableArray<Diagnostic> Diagnostics
    ) : TargetInterfaceValidationResult;
}
