using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.InlineInterface.SourceGenerationHelpers;

namespace Macaron.InlineInterface;

[Generator]
public sealed class InlineInterfaceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeSymbolProvider = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is InvocationExpressionSyntax,
                transform: static (generatorSyntaxContext, _) => TargetTypeExtractor.Discover(generatorSyntaxContext)
            )
            .Where(static result => result is not TargetTypeDiscoveryResult.NotApplicable);

        var validatedProvider = typeSymbolProvider
            .Collect()
            .SelectMany(static (results, _) =>
            {
                var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var builder = ImmutableArray.CreateBuilder<TargetInterfaceValidationResult>();

                foreach (var result in results)
                {
                    switch (result)
                    {
                        case TargetTypeDiscoveryResult.Failure failure:
                        {
                            builder.Add(new TargetInterfaceValidationResult.Failure(
                                Diagnostics: ImmutableArray.Create(failure.Diagnostic)
                            ));

                            break;
                        }
                        case TargetTypeDiscoveryResult.Success success:
                        {
                            if (!seenTypes.Add(success.Symbol))
                            {
                                continue;
                            }

                            builder.Add(InterfaceValidator.ValidateTargetInterface(success.Symbol, success.Syntax));

                            break;
                        }
                    }
                }

                return builder.ToImmutable();
            });

        context.RegisterSourceOutput(
            source: validatedProvider,
            action: (sourceProductionContext, validationResult) =>
            {
                switch (validationResult)
                {
                    case TargetInterfaceValidationResult.Failure failure:
                    {
                        foreach (var diagnostic in failure.Diagnostics)
                        {
                            sourceProductionContext.ReportDiagnostic(diagnostic);
                        }

                        break;
                    }
                    case TargetInterfaceValidationResult.Success success:
                    {
                        var (interfaceSymbol, contexts) = success;

                        AddSource(sourceProductionContext, interfaceSymbol, contexts);

                        break;
                    }
                }
            }
        );
    }
}
