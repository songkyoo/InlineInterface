using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

public sealed class EventContextProvider(
    IEnumerable<IEventSymbol> eventSymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
    InterfaceTypeStringProvider interfaceTypeStringProvider,
    string indent
)
{
    #region Static Methods
    private static Dictionary<string, List<EventContext>> CreateCache(
        IEnumerable<IEventSymbol> eventSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var cache = new Dictionary<string, List<EventContext>>();

        foreach (var eventSymbol in eventSymbols)
        {
            var eventName = eventSymbol.Name;

            if (!cache.TryGetValue(eventName, out var contexts))
            {
                contexts = [];
                cache.Add(eventSymbol.Name, contexts);
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

        return cache;
    }
    #endregion

    #region Fields
    private readonly Dictionary<string, List<EventContext>> _cache = CreateCache(eventSymbols, genericParameterMap);
    #endregion

    #region Properties
    public IEnumerable<EventContext> Contexts => _cache.Values.SelectMany(x => x);
    #endregion

    #region Methods
    public ImmutableArray<string> GetEventDispatcherImplementation(IEventSymbol eventSymbol)
    {
        if (!TryGetEventContext(eventSymbol, out var context))
        {
            return ImmutableArray<string>.Empty;
        }

        var methodSymbol = context.TypeSymbol.DelegateInvokeMethod!;

        string returnString;

        if (methodSymbol.ReturnsVoid)
        {
            returnString = "void Raise";
        }
        else
        {
            var returnTypeString = SymbolHelpers.GetTypeString(methodSymbol.ReturnType, genericParameterMap);

            if (!returnTypeString.EndsWith("?"))
            {
                returnTypeString += "?";
            }

            returnString = $"{returnTypeString} Invoke";
        }

        var parameters = new List<string>();
        var arguments = new List<string>();

        foreach (var paramSymbol in methodSymbol.Parameters)
        {
            var (type, name) = GetParameterString(paramSymbol, genericParameterMap);

            parameters.Add($"{type} {name}");
            arguments.Add(name);
        }

        var implementationBuilder = ImmutableArray.CreateBuilder<string>();

        implementationBuilder.Add($"public {returnString}{context.Name}({string.Join(", ", parameters)})");
        implementationBuilder.Add($"{{");
        implementationBuilder.Add($"{indent}if (_eventCollection.{context.UniqueName} == null) return{(methodSymbol.ReturnsVoid ? "" : " default")};");
        implementationBuilder.Add($"{indent}{(methodSymbol.ReturnsVoid ? "" : "return ")}_eventCollection.{context.UniqueName}({string.Join(", ", arguments)});");
        implementationBuilder.Add($"}}");

        return implementationBuilder.ToImmutable();
    }

    public ImmutableArray<string> GetInterfaceImplementation(IEventSymbol eventSymbol)
    {
        if (!TryGetEventContext(eventSymbol, out var context))
        {
            return ImmutableArray<string>.Empty;
        }

        var interfaceTypeString = interfaceTypeStringProvider.GetInterfaceTypeName(eventSymbol.ContainingType);
        var implementationBuilder = ImmutableArray.CreateBuilder<string>();

        implementationBuilder.Add($"event {context.Type} {interfaceTypeString}.{context.Name}");
        implementationBuilder.Add($"{{");
        implementationBuilder.Add($"{indent}add => _eventCollection.{context.UniqueName} += value;");
        implementationBuilder.Add($"{indent}remove => _eventCollection.{context.UniqueName} -= value;");
        implementationBuilder.Add($"}}");

        return implementationBuilder.ToImmutable();
    }

    private bool TryGetEventContext(IEventSymbol eventSymbol, [NotNullWhen(returnValue: true)]out EventContext? context)
    {
        if (!_cache.TryGetValue(eventSymbol.Name, out var contexts))
        {
            context = null;

            return false;
        }

        var index = contexts.FindIndex(x => SymbolEqualityComparer.Default.Equals(x.TypeSymbol, eventSymbol.Type));

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
