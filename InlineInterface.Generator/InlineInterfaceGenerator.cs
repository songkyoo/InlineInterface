using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.SourceGenerationHelpers;

namespace Macaron.InlineInterface;

[Generator]
public sealed class InlineInterfaceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 대상 호출 판별
        var typeSymbolProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => TargetTypeExtractor.IsCandidate(syntaxNode),
                transform: static (generatorSyntaxContext, _) => TargetTypeExtractor.Discover(generatorSyntaxContext)
            )
            .Where(static result => result is not TargetTypeDiscoveryResult.NotApplicable);

        context.RegisterSourceOutput(
            source: typeSymbolProvider
                .Where(static result => result is TargetTypeDiscoveryResult.Failure)
                .Select(static (result, _) => (TargetTypeDiscoveryResult.Failure)result),
            action: static (sourceProductionContext, failure) =>
            {
                sourceProductionContext.ReportDiagnostic(failure.Diagnostic);
            }
        );

        var validatedProvider = typeSymbolProvider
            .Where(static result => result is TargetTypeDiscoveryResult.Success)
            .Select(static (result, _) => (TargetTypeDiscoveryResult.Success)result)
            .Collect()
            .SelectMany(static (results, _) =>
            {
                var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var builder = new List<TargetInterfaceValidationResult>();

                foreach (var success in results)
                {
                    if (!seenTypes.Add(success.Symbol))
                    {
                        continue;
                    }

                    builder.Add(InterfaceValidator.ValidateTargetInterface(success.Symbol, success.Syntax));
                }

                return builder;
            });

        context.RegisterSourceOutput(
            source: validatedProvider
                .Where(static result => result is TargetInterfaceValidationResult.Failure)
                .Select(static (result, _) => (TargetInterfaceValidationResult.Failure)result),
            action: static (sourceProductionContext, failure) =>
            {
                foreach (var diagnostic in failure.Diagnostics)
                {
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
