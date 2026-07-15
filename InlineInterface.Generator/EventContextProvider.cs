using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

internal sealed class EventContextProvider(
    IEnumerable<IEventSymbol> eventSymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
)
{
    #region Nested Types
    private sealed record ProviderCache(
        ImmutableSortedDictionary<string, ImmutableArray<EventContext>> Contexts,
        ImmutableArray<EventGenerationModel> Models
    );
    #endregion

    #region Static Methods
    private static ProviderCache CreateCache(
        IEnumerable<IEventSymbol> eventSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var builder = new SortedDictionary<string, List<EventContext>>();

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
                continue;
            }

            if (typeSymbol.TypeKind != TypeKind.Delegate || typeSymbol.DelegateInvokeMethod is null)
            {
                continue;
            }

            if (contexts.Any(x => SymbolEqualityComparer.Default.Equals(typeSymbol, x.TypeSymbol)))
            {
                continue;
            }

            var type = SymbolHelpers.GetTypeString(typeSymbol, genericParameterMap);

            if (!type.EndsWith("?"))
            {
                type += "?";
            }

            var uniqueName = $"{eventName}_{contexts.Count}";
            var newContext = new EventContext(
                TypeSymbol: typeSymbol,
                Model: CreateGenerationModel(
                    typeSymbol,
                    type,
                    eventName,
                    uniqueName,
                    genericParameterMap
                )
            );

            contexts.Add(newContext);
        }

        var contextCacheBuilder = ImmutableSortedDictionary.CreateBuilder<string, ImmutableArray<EventContext>>();
        var modelBuilder = ImmutableArray.CreateBuilder<EventGenerationModel>();

        foreach (var pair in builder)
        {
            var contexts = pair.Value;
            var contextBuilder = ImmutableArray.CreateBuilder<EventContext>(contexts.Count);

            foreach (var context in contexts)
            {
                contextBuilder.Add(context with { ModelIndex = modelBuilder.Count });
                modelBuilder.Add(context.Model);
            }

            contextCacheBuilder.Add(pair.Key, contextBuilder.ToImmutable());
        }

        return new ProviderCache(
            Contexts: contextCacheBuilder.ToImmutable(),
            Models: modelBuilder.ToImmutable()
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
        var parameters = new List<string>();
        var arguments = new List<string>();

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
    #endregion

    #region Methods
    public bool TryGetEventModelIndex(
        IEventSymbol eventSymbol,
        out int modelIndex
    )
    {
        if (!_cache.Contexts.TryGetValue(eventSymbol.Name, out var contexts))
        {
            modelIndex = -1;

            return false;
        }

        var index = -1;

        for (var i = 0; i < contexts.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(contexts[i].TypeSymbol, eventSymbol.Type))
            {
                index = i;

                break;
            }
        }

        if (index == -1)
        {
            modelIndex = -1;

            return false;
        }

        modelIndex = contexts[index].ModelIndex;

        return true;
    }
    #endregion
}
