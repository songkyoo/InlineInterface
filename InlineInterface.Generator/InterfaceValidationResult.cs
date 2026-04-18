using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

internal abstract record InterfaceValidationResult
{
    public sealed record Success(
        INamedTypeSymbol InterfaceSymbol,
        ImmutableArray<InterfaceContext> Contexts
    ) : InterfaceValidationResult;

    public sealed record Failure(
        ImmutableArray<Diagnostic> Diagnostics
    ) : InterfaceValidationResult;
}
