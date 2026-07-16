using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ImplementationBuilderAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArray.Create(
        InlineInterfaceDiagnostics.BuilderMustBeCompletedInSameExpressionRule,
        InlineInterfaceDiagnostics.MissingRequiredBuilderMembersRule
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationStartContext =>
        {
            var requiredMemberProvider = new RequiredBuilderMemberProvider();

            compilationStartContext.RegisterSyntaxNodeAction(
                context => AnalyzeInvocation(context, requiredMemberProvider),
                SyntaxKind.InvocationExpression
            );
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        RequiredBuilderMemberProvider requiredMemberProvider
    )
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            !TargetTypeExtractor.IsCandidate(invocation) ||
            context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation ||
            !TryGetImplementationOfTarget(operation, invocation, out var target)
        )
        {
            return;
        }

        if (!TryCollectInvocationChain(
            invocation,
            out var chainInvocations,
            out var outermostExpression
        ))
        {
            return;
        }

        var lastInvocation = chainInvocations[^1];

        if (!IsBuildInvocation(lastInvocation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor: InlineInterfaceDiagnostics.BuilderMustBeCompletedInSameExpressionRule,
                location: outermostExpression.GetLocation()
            ));

            return;
        }

        if (!TryCollectInvocationOperationChain(operation, chainInvocations, out var chainOperations) ||
            !IsBuildInvocation(chainOperations[^1], target.InterfaceSymbol)
        )
        {
            return;
        }

        var allowMissingImplementation = GetAllowMissingImplementation(operation);

        if (allowMissingImplementation is not false)
        {
            return;
        }

        if (!requiredMemberProvider.TryGetRequiredMembers(
            target.InterfaceSymbol,
            context.CancellationToken,
            out var requiredMembers
        ))
        {
            return;
        }

        var configuredMemberSignatures = new HashSet<BuilderMemberSignature>(
            BuilderMemberSignatureComparer.Instance
        );

        for (var i = 1; i < chainInvocations.Length - 1; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (BuilderMemberSignatureFactory.TryCreate(
                chainOperations[i].TargetMethod,
                out var signature
            ))
            {
                configuredMemberSignatures.Add(signature);
            }
        }

        ImmutableArray<string>.Builder? missingMembersBuilder = null;

        foreach (var member in requiredMembers)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (configuredMemberSignatures.Contains(member.Signature))
            {
                continue;
            }

            missingMembersBuilder ??= ImmutableArray.CreateBuilder<string>();
            missingMembersBuilder.Add(member.CreateDescription(context.CancellationToken));
        }

        if (missingMembersBuilder is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor: InlineInterfaceDiagnostics.MissingRequiredBuilderMembersRule,
            location: lastInvocation.GetLocation(),
            messageArgs: [
                target.InterfaceSymbol.ToDisplayString(MinimallyQualifiedFormat),
                string.Join(", ", missingMembersBuilder),
            ]
        ));
    }

    private static bool IsBuildInvocation(InvocationExpressionSyntax invocation)
    {
        return GetInvokedMethodName(invocation) == "Build";
    }

    private static bool IsBuildInvocation(
        IInvocationOperation invocation,
        INamedTypeSymbol targetInterfaceSymbol
    )
    {
        return invocation.TargetMethod.Name == "Build" &&
               SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ReturnType, targetInterfaceSymbol);
    }

    private static bool TryCollectInvocationChain(
        InvocationExpressionSyntax invocation,
        out ImmutableArray<InvocationExpressionSyntax> invocations,
        out ExpressionSyntax outermostExpression
    )
    {
        var builder = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        builder.Add(invocation);

        var current = invocation;

        while (true)
        {
            switch (current.Parent)
            {
                case MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == current:
                {
                    if (memberAccess.Parent is not InvocationExpressionSyntax parentInvocation ||
                        parentInvocation.Expression != memberAccess
                    )
                    {
                        invocations = ImmutableArray<InvocationExpressionSyntax>.Empty;
                        outermostExpression = memberAccess;

                        return false;
                    }

                    builder.Add(parentInvocation);
                    current = parentInvocation;

                    continue;
                }
                case InvocationExpressionSyntax parentInvocation when parentInvocation.Expression == current:
                {
                    invocations = ImmutableArray<InvocationExpressionSyntax>.Empty;
                    outermostExpression = parentInvocation;

                    return false;
                }
                default:
                {
                    invocations = builder.ToImmutable();
                    outermostExpression = current;

                    return true;
                }
            }
        }
    }

    private static bool TryCollectInvocationOperationChain(
        IInvocationOperation operation,
        ImmutableArray<InvocationExpressionSyntax> invocationSyntaxes,
        out ImmutableArray<IInvocationOperation> invocations
    )
    {
        if (invocationSyntaxes.Length == 0 || !HasSameSyntax(operation, invocationSyntaxes[0]))
        {
            invocations = ImmutableArray<IInvocationOperation>.Empty;

            return false;
        }

        var builder = ImmutableArray.CreateBuilder<IInvocationOperation>(invocationSyntaxes.Length);
        builder.Add(operation);

        IOperation current = operation;

        for (var i = 1; i < invocationSyntaxes.Length; i++)
        {
            var expectedSyntax = invocationSyntaxes[i];
            IOperation? parent = current.Parent;

            while (parent is not null &&
                   (parent is not IInvocationOperation || !HasSameSyntax(parent, expectedSyntax))
            )
            {
                parent = parent.Parent;
            }

            if (parent is not IInvocationOperation parentInvocation)
            {
                invocations = ImmutableArray<IInvocationOperation>.Empty;

                return false;
            }

            builder.Add(parentInvocation);
            current = parentInvocation;
        }

        invocations = builder.ToImmutable();

        return true;
    }

    private static bool HasSameSyntax(IOperation operation, SyntaxNode syntaxNode)
    {
        return operation.Syntax.SyntaxTree == syntaxNode.SyntaxTree && operation.Syntax.Span == syntaxNode.Span;
    }

    private static bool TryGetImplementationOfTarget(
        IInvocationOperation operation,
        InvocationExpressionSyntax invocation,
        out ImplementationOfTarget target
    )
    {
        target = default;

        if (TargetTypeExtractor.GetCandidateGenericName(invocation) is not { } genericNameSyntax ||
            operation.TargetMethod is not { IsStatic: true, Name: "Of" } methodSymbol ||
            !TargetTypeExtractor.IsImplementationType(methodSymbol.ContainingType) ||
            genericNameSyntax.TypeArgumentList.Arguments is not [_] ||
            methodSymbol.TypeArguments is not [{ } typeArgument] ||
            typeArgument is not INamedTypeSymbol interfaceSymbol
        )
        {
            return false;
        }

        target = new ImplementationOfTarget(interfaceSymbol);

        return true;
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: { } name } => name.Identifier.ValueText,
            SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };
    }

    private static bool? GetAllowMissingImplementation(IInvocationOperation operation)
    {
        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter?.Name != "allowMissingImplementation")
            {
                continue;
            }

            return argument.Value.ConstantValue is { HasValue: true, Value: bool value }
                ? value
                : null;
        }

        return false;
    }

    private readonly record struct ImplementationOfTarget(INamedTypeSymbol InterfaceSymbol);
}
