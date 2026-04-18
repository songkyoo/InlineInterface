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
                transform: static (generatorSyntaxContext, _) => TargetTypeExtractor.Extract(generatorSyntaxContext)
            )
            .Where(static result => result is not TargetTypeExtractionResult.NotApplicable);

        var validatedProvider = typeSymbolProvider
            .Collect()
            .SelectMany(static (results, _) =>
            {
                var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                var builder = ImmutableArray.CreateBuilder<InterfaceValidationResult>();

                foreach (var result in results)
                {
                    switch (result)
                    {
                        case TargetTypeExtractionResult.Failure failure:
                        {
                            builder.Add(new InterfaceValidationResult.Failure(
                                Diagnostics: ImmutableArray.Create(failure.Diagnostic)
                            ));

                            break;
                        }
                        case TargetTypeExtractionResult.Success success:
                        {
                            if (!seenTypes.Add(success.Symbol))
                            {
                                continue;
                            }

                            builder.Add(InterfaceValidator.ValidateTypeSymbol(success.Symbol, success.Syntax));

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
                    case InterfaceValidationResult.Failure failure:
                    {
                        foreach (var diagnostic in failure.Diagnostics)
                        {
                            sourceProductionContext.ReportDiagnostic(diagnostic);
                        }

                        break;
                    }
                    case InterfaceValidationResult.Success success:
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
