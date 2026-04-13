using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

public sealed class PropertyContextProvider(
    IEnumerable<IPropertySymbol> propertySymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
    InterfaceTypeStringProvider interfaceTypeStringProvider,
    string globalTypeBuilder,
    bool hasEventMembers,
    string indent
)
{
    #region Static Methods
    private static ImmutableSortedDictionary<string, ImmutableArray<PropertyContext>> CreateCache(
        IEnumerable<IPropertySymbol> propertySymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string globalTypeBuilder,
        bool hasEventMembers
    )
    {
        var builder = new Dictionary<string, List<PropertyContext>>();

        foreach (var propertySymbol in propertySymbols)
        {
            var propertyName = propertySymbol.Name;

            if (!builder.TryGetValue(propertyName, out var contexts))
            {
                contexts = [];
                builder.Add(propertyName, contexts);
            }

            var index = -1;

            for (var i = 0; i < contexts.Count; i++)
            {
                if (MatchesPropertySignature(
                    propertySymbol,
                    contexts[i].Name,
                    contexts[i].TypeSymbol,
                    contexts[i].ParameterSymbols
                ))
                {
                    index = i;

                    break;
                }
            }

            if (index == -1)
            {
                contexts.Add(CreateContext(
                    propertySymbol,
                    genericParameterMap,
                    globalTypeBuilder,
                    hasEventMembers,
                    contexts.Count
                ));
            }
            else
            {
                var existing = contexts[index];
                var created = CreateContext(
                    propertySymbol,
                    genericParameterMap,
                    globalTypeBuilder,
                    hasEventMembers,
                    index
                );

                contexts[index] = existing with
                {
                    RequiresGetter = existing.RequiresGetter || created.RequiresGetter,
                    RequiresSetter = existing.RequiresSetter || created.RequiresSetter,
                    GetterDelegateType = existing.GetterDelegateType ?? created.GetterDelegateType,
                    SetterDelegateType = existing.SetterDelegateType ?? created.SetterDelegateType,
                    GetterName = existing.GetterName ?? created.GetterName,
                    SetterName = existing.SetterName ?? created.SetterName,
                    GetterParameterName = existing.GetterParameterName ?? created.GetterParameterName,
                    SetterParameterName = existing.SetterParameterName ?? created.SetterParameterName,
                    GetterFieldName = existing.GetterFieldName ?? created.GetterFieldName,
                    SetterFieldName = existing.SetterFieldName ?? created.SetterFieldName,
                };
            }
        }

        return builder.ToImmutableSortedDictionary(
            keySelector: x => x.Key,
            elementSelector: x => x.Value.ToImmutableArray()
        );
    }

    private static bool MatchesPropertySignature(
        IPropertySymbol propertySymbol,
        string propertyName,
        ITypeSymbol typeSymbol,
        ImmutableArray<IParameterSymbol> parameterSymbols
    )
    {
        var comparer = SymbolEqualityComparer.Default;

        if (!propertyName.Equals(propertySymbol.Name))
        {
            return false;
        }

        if (!comparer.Equals(typeSymbol, propertySymbol.Type))
        {
            return false;
        }

        if (parameterSymbols.Length != propertySymbol.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < parameterSymbols.Length; i++)
        {
            var left = parameterSymbols[i];
            var right = propertySymbol.Parameters[i];

            if (!comparer.Equals(left.Type, right.Type))
            {
                return false;
            }

            if (left.RefKind != right.RefKind)
            {
                return false;
            }

            if (left.IsParams != right.IsParams)
            {
                return false;
            }
        }

        return true;
    }

    private static PropertyContext CreateContext(
        IPropertySymbol propertySymbol,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string globalTypeBuilder,
        bool hasEventMembers,
        int uniqueIndex
    )
    {
        var propertyType = SymbolHelpers.GetTypeString(propertySymbol.Type, genericParameterMap);

        var isIndexer = propertySymbol.IsIndexer;
        var apiName = isIndexer ? "Indexer" : propertySymbol.Name;
        var uniqueName = isIndexer ? $"Indexer_{uniqueIndex}" : $"{propertySymbol.Name}_{uniqueIndex}";

        var parameters = new List<string>();
        var arguments = new List<string>();
        var delegateParameterTypes = new List<string>();

        if (hasEventMembers)
        {
            delegateParameterTypes.Add($"{globalTypeBuilder}.EventDispatcher");
            arguments.Add("_eventDispatcher");
        }

        foreach (var parameterSymbol in propertySymbol.Parameters)
        {
            var (type, name) = GetParameterString(parameterSymbol, genericParameterMap);

            parameters.Add($"{type} {name}");
            arguments.Add(name);
            delegateParameterTypes.Add(type);
        }

        var parameterList = string.Join(", ", parameters);
        var argumentList = string.Join(", ", arguments);

        string? getterDelegateType;
        string? setterDelegateType;
        string? getterName;
        string? setterName;
        string? getterParameterName;
        string? setterParameterName;
        string? getterFieldName;
        string? setterFieldName;

        if (propertySymbol.GetMethod != null)
        {
            getterName = $"Property_Get_{uniqueName}";
            getterParameterName = $"property_get_{uniqueName}";
            getterFieldName = $"_{getterParameterName}";

            getterDelegateType = delegateParameterTypes.Count > 0
                ? $"global::System.Func<{string.Join(", ", delegateParameterTypes)}, {propertyType}>"
                : $"global::System.Func<{propertyType}>";
        }
        else
        {
            getterDelegateType = null;
            getterName = null;
            getterParameterName = null;
            getterFieldName = null;
        }

        if (propertySymbol.SetMethod != null)
        {
            setterName = $"Property_Set_{uniqueName}";
            setterParameterName = $"property_set_{uniqueName}";
            setterFieldName = $"_{setterParameterName}";

            var setterDelegateParameterTypes = delegateParameterTypes
                .Concat([propertyType])
                .ToArray();

            setterDelegateType = setterDelegateParameterTypes.Length > 0
                ? $"global::System.Action<{string.Join(", ", setterDelegateParameterTypes)}>"
                : $"global::System.Action";
        }
        else
        {
            setterDelegateType = null;
            setterName = null;
            setterParameterName = null;
            setterFieldName = null;
        }

        return new PropertyContext(
            TypeSymbol: propertySymbol.Type,
            ParameterSymbols: propertySymbol.Parameters,
            IsIndexer: isIndexer,
            Type: propertyType,
            Name: propertySymbol.Name,
            ApiName: apiName,
            Parameters: parameterList,
            Arguments: argumentList,
            RequiresGetter: propertySymbol.GetMethod != null,
            RequiresSetter: propertySymbol.SetMethod != null,
            GetterDelegateType: getterDelegateType,
            SetterDelegateType: setterDelegateType,
            GetterName: getterName,
            SetterName: setterName,
            GetterParameterName: getterParameterName,
            SetterParameterName: setterParameterName,
            GetterFieldName: getterFieldName,
            SetterFieldName: setterFieldName
        );
    }
    #endregion

    #region Fields
    private readonly ImmutableSortedDictionary<string, ImmutableArray<PropertyContext>> _cache = CreateCache(
        propertySymbols,
        genericParameterMap,
        globalTypeBuilder,
        hasEventMembers
    );
    #endregion

    #region Properties
    public IEnumerable<PropertyContext> Contexts => _cache.Values.SelectMany(x => x);
    #endregion

    #region Methods
    public ImmutableArray<string> GetInterfaceImplementation(IPropertySymbol propertySymbol)
    {
        if (!TryGetPropertyContext(propertySymbol, out var context))
        {
            return ImmutableArray<string>.Empty;
        }

        var interfaceTypeString = interfaceTypeStringProvider.GetInterfaceTypeName(propertySymbol.ContainingType);
        var implementationBuilder = ImmutableArray.CreateBuilder<string>();

        var propertyName = context.IsIndexer ? "this" : context.Name;
        var parameterList = context.Parameters.Length > 0
            ? $"[{context.Parameters}]"
            : "";

        implementationBuilder.Add($"{context.Type} {interfaceTypeString}.{propertyName}{parameterList}");
        implementationBuilder.Add("{");

        if (propertySymbol.GetMethod != null)
        {
            var getterArguments = context.Arguments;

            implementationBuilder.Add($"{indent}get => ({context.GetterFieldName} ?? throw new global::System.NotImplementedException())({getterArguments});");
        }

        if (propertySymbol.SetMethod != null)
        {
            var setterArguments = string.IsNullOrEmpty(context.Arguments)
                ? "value"
                : $"{context.Arguments}, value";

            implementationBuilder.Add($"{indent}set => ({context.SetterFieldName} ?? throw new global::System.NotImplementedException())({setterArguments});");
        }

        implementationBuilder.Add("}");

        return implementationBuilder.ToImmutable();
    }

    private bool TryGetPropertyContext(
        IPropertySymbol propertySymbol,
        [NotNullWhen(returnValue: true)] out PropertyContext? context
    )
    {
        if (!_cache.TryGetValue(propertySymbol.Name, out var contexts))
        {
            context = null;

            return false;
        }

        var index = -1;

        for (var i = 0; i < contexts.Length; i++)
        {
            if (MatchesPropertySignature(
                propertySymbol,
                contexts[i].Name,
                contexts[i].TypeSymbol,
                contexts[i].ParameterSymbols
            ))
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
