using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

internal sealed class EventContextProvider(
    ImmutableArray<IEventSymbol> eventSymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
)
{
    #region Types
    private sealed record ProviderCache(
        ImmutableArray<EventGenerationModel> Models,
        ImmutableArray<int> GenerationModelIndicesByImplementation
    );
    #endregion

    #region Static Methods
    private static ProviderCache CreateCache(
        ImmutableArray<IEventSymbol> eventSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var builder = new SortedDictionary<string, List<EventContext>>();
        var implementationContexts = new List<EventContext?>(capacity: eventSymbols.Length);

        foreach (var eventSymbol in eventSymbols)
        {
            var eventName = eventSymbol.Name;

            if (!builder.TryGetValue(eventName, out var contexts))
            {
                contexts = [];
                builder.Add(eventSymbol.Name, contexts);
            }

            if (eventSymbol.Type is not INamedTypeSymbol typeSymbol)
            {
                implementationContexts.Add(null);

                continue;
            }

            if (typeSymbol.TypeKind != TypeKind.Delegate || typeSymbol.DelegateInvokeMethod is null)
            {
                implementationContexts.Add(null);

                continue;
            }

            EventContext? context = null;

            foreach (var existingContext in contexts)
            {
                if (SymbolEqualityComparer.Default.Equals(typeSymbol, existingContext.TypeSymbol))
                {
                    context = existingContext;

                    break;
                }
            }

            if (context is null)
            {
                var type = SymbolHelpers.GetTypeString(typeSymbol, genericParameterMap);

                if (!type.EndsWith("?"))
                {
                    type += "?";
                }

                var uniqueName = $"{eventName}_{contexts.Count}";
                context = new EventContext(
                    typeSymbol,
                    model: CreateGenerationModel(
                        typeSymbol,
                        type,
                        eventName,
                        uniqueName,
                        genericParameterMap
                    )
                );

                contexts.Add(context);
            }

            implementationContexts.Add(context);
        }

        var modelBuilder = ImmutableArray.CreateBuilder<EventGenerationModel>(initialCapacity: eventSymbols.Length);

        foreach (var pair in builder)
        {
            foreach (var context in pair.Value)
            {
                context.ModelIndex = modelBuilder.Count;
                modelBuilder.Add(context.Model);
            }
        }

        var indexBuilder = ImmutableArray.CreateBuilder<int>(initialCapacity: implementationContexts.Count);

        foreach (var context in implementationContexts)
        {
            indexBuilder.Add(context?.ModelIndex ?? -1);
        }

        return new ProviderCache(
            Models: modelBuilder.ToImmutable(),
            GenerationModelIndicesByImplementation: indexBuilder.ToImmutable()
        );
    }

    private static EventGenerationModel CreateGenerationModel(
        INamedTypeSymbol typeSymbol,
        string type,
        string name,
        string uniqueName,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var methodSymbol = typeSymbol.DelegateInvokeMethod!;
        var parameters = new List<string>(
            capacity: methodSymbol.Parameters.Length + (methodSymbol.ReturnsVoid ? 0 : 1)
        );
        var arguments = new List<string>(methodSymbol.Parameters.Length);

        foreach (var parameterSymbol in methodSymbol.Parameters)
        {
            var (parameterType, parameterName) = GetParameterString(
                parameterSymbol,
                genericParameterMap,
                includeModifier: true
            );

            parameters.Add($"{parameterType} {parameterName}");
            arguments.Add(GetArgumentString(parameterSymbol, includeModifier: true));
        }

        if (!methodSymbol.ReturnsVoid)
        {
            var returnType = SymbolHelpers.GetTypeString(methodSymbol.ReturnType, genericParameterMap);

            if (!returnType.EndsWith("?"))
            {
                returnType += "?";
            }

            parameters.Add($"out {returnType} @return");
        }

        return new EventGenerationModel(
            Type: type,
            Name: name,
            UniqueName: uniqueName,
            DispatcherParameters: string.Join(", ", parameters),
            DispatcherArguments: string.Join(", ", arguments),
            ReturnsVoid: methodSymbol.ReturnsVoid
        );
    }
    #endregion

    #region Fields
    private readonly ProviderCache _cache = CreateCache(
        eventSymbols,
        genericParameterMap
    );
    #endregion

    #region Properties
    public ImmutableArray<EventGenerationModel> Models => _cache.Models;
    public ImmutableArray<int> GenerationModelIndicesByImplementation =>
        _cache.GenerationModelIndicesByImplementation;
    #endregion
}
