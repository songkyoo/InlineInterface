using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface;

internal abstract record TargetTypeExtractionResult
{
    private TargetTypeExtractionResult() { }

    public sealed record Success(
        INamedTypeSymbol Symbol,
        TypeSyntax Syntax
    ) : TargetTypeExtractionResult;

    public sealed record Failure(
        Diagnostic Diagnostic
    ) : TargetTypeExtractionResult;

    public sealed record NotApplicable : TargetTypeExtractionResult;
}
