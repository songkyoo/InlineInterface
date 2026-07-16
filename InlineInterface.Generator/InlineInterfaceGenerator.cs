using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.SourceGenerationHelpers;

namespace Macaron.InlineInterface;

[Generator]
public sealed class InlineInterfaceGenerator : IIncrementalGenerator
{
    internal const string CollectedTargetsTrackingName = "CollectedTargets";
    internal const string ValidationTrackingName = "TargetInterfaceValidation";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 대상 호출 판별
        var typeSymbolProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => TargetTypeExtractor.IsCandidate(syntaxNode),
                transform: static (generatorSyntaxContext, cancellationToken) =>
                    TargetTypeExtractor.Discover(generatorSyntaxContext, cancellationToken)
            )
            .Where(static result => result is not TargetTypeDiscoveryResult.NotApplicable);

        context.RegisterSourceOutput(
            source: typeSymbolProvider
                .Where(static result => result is TargetTypeDiscoveryResult.Failure)
                .Select(static (result, _) => (TargetTypeDiscoveryResult.Failure)result),
            action: static (sourceProductionContext, failure) =>
            {
                sourceProductionContext.CancellationToken.ThrowIfCancellationRequested();
                sourceProductionContext.ReportDiagnostic(failure.Diagnostic);
            }
        );

        var collectedTargetsProvider = typeSymbolProvider
            .Where(static result => result is TargetTypeDiscoveryResult.Success)
            .Select(static (result, _) => (TargetTypeDiscoveryResult.Success)result)
            .Collect()
            .WithTrackingName(CollectedTargetsTrackingName);

        var validatedProvider = collectedTargetsProvider
            .SelectMany(static (results, cancellationToken) =>
            {
                var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var builder = new List<TargetInterfaceValidationResult>();

                foreach (var success in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!seenTypes.Add(success.Symbol))
                    {
                        continue;
                    }

                    builder.Add(InterfaceValidator.ValidateTargetInterface(
                        success.Symbol,
                        success.Syntax,
                        cancellationToken
                    ));
                }

                return builder;
            })
            .WithTrackingName(ValidationTrackingName);

        context.RegisterSourceOutput(
            source: validatedProvider
                .Where(static result => result is TargetInterfaceValidationResult.Failure)
                .Select(static (result, _) => (TargetInterfaceValidationResult.Failure)result),
            action: static (sourceProductionContext, failure) =>
            {
                foreach (var diagnostic in failure.Diagnostics)
                {
                    sourceProductionContext.CancellationToken.ThrowIfCancellationRequested();
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }
            }
        );

        var generationModelProvider = validatedProvider
            .Where(static result => result is TargetInterfaceValidationResult.Success)
            .Select(static (result, _) => (TargetInterfaceValidationResult.Success)result)
            .Select(static (success, _) => InterfaceGenerationModelFactory.Create(
                success.InterfaceSymbol,
                success.Contexts
            ))
            .WithTrackingName(nameof(InterfaceGenerationModel));

        context.RegisterSourceOutput(
            source: generationModelProvider,
            action: static (sourceProductionContext, model) => AddSource(sourceProductionContext, model)
        );
    }
}
