using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.InlineInterface;

internal abstract record TargetTypeDiscoveryResult
{
    private TargetTypeDiscoveryResult() { }

    public sealed record Success(
        INamedTypeSymbol Symbol,
        TypeSyntax Syntax
    ) : TargetTypeDiscoveryResult;

    public sealed record Failure(
        Diagnostic Diagnostic
    ) : TargetTypeDiscoveryResult;

    public sealed record NotApplicable : TargetTypeDiscoveryResult;
}
