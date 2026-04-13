using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed class EventContextProvider(
    IEnumerable<IEventSymbol> eventSymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
)
{
    #region Static Methods
    private static ImmutableSortedDictionary<string, ImmutableArray<EventContext>> CreateCache(
        IEnumerable<IEventSymbol> eventSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var builder = new Dictionary<string, List<EventContext>>();

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

            if (eventSymbol.NullableAnnotation != NullableAnnotation.Annotated)
            {
                type += "?";
            }

            var newContext = new EventContext(
                TypeSymbol: typeSymbol,
                Type: type,
                Name: eventName,
                UniqueName: $"{eventName}_{contexts.Count}"
            );

            contexts.Add(newContext);
        }

        return builder.ToImmutableSortedDictionary(
            keySelector: x => x.Key,
            elementSelector: x => x.Value.ToImmutableArray()
        );
    }
    #endregion

    #region Fields
    private readonly ImmutableSortedDictionary<string, ImmutableArray<EventContext>> _cache = CreateCache(
        eventSymbols,
        genericParameterMap
    );
    #endregion

    #region Properties
    public IEnumerable<EventContext> Contexts => _cache.Values.SelectMany(x => x);
    #endregion

    #region Methods
    public bool TryGetEventContext(IEventSymbol eventSymbol, [NotNullWhen(returnValue: true)]out EventContext? context)
    {
        if (!_cache.TryGetValue(eventSymbol.Name, out var contexts))
        {
            context = null;

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
            context = null;

            return false;
        }

        context = contexts[index];

        return true;
    }
    #endregion
}
