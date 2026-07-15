using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

internal sealed class EventContextProvider(
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

        return builder.ToImmutableSortedDictionary(
            keySelector: x => x.Key,
            elementSelector: x => x.Value.ToImmutableArray()
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
    private readonly ImmutableSortedDictionary<string, ImmutableArray<EventContext>> _cache = CreateCache(
        eventSymbols,
        genericParameterMap
    );
    #endregion

    #region Properties
    public IEnumerable<EventGenerationModel> Models => _cache
        .Values
        .SelectMany(static contexts => contexts)
        .Select(static context => context.Model);
    #endregion

    #region Methods
    public bool TryGetEventModel(
        IEventSymbol eventSymbol,
        [NotNullWhen(returnValue: true)]out EventGenerationModel? model
    )
    {
        if (!_cache.TryGetValue(eventSymbol.Name, out var contexts))
        {
            model = null;

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
            model = null;

            return false;
        }

        model = contexts[index].Model;

        return true;
    }
    #endregion
}
