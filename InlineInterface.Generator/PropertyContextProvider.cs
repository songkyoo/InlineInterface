using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

internal sealed class PropertyContextProvider(
    IEnumerable<IPropertySymbol> propertySymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
    string globalTypeBuilder,
    bool hasEventMembers
)
{
    #region Nested Types
    private sealed record ProviderCache(
        ImmutableArray<PropertyGenerationModel> Models,
        ImmutableArray<int> GenerationModelIndicesByImplementation
    );
    #endregion

    #region Static Methods
    private static ProviderCache CreateCache(
        IEnumerable<IPropertySymbol> propertySymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string globalTypeBuilder,
        bool hasEventMembers
    )
    {
        var builder = new SortedDictionary<string, List<PropertyContext>>();
        var implementationContexts = new List<PropertyContext>();

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
                    contexts[i].Model.Name,
                    contexts[i].TypeSymbol,
                    contexts[i].ParameterSymbols
                ))
                {
                    index = i;

                    break;
                }
            }

            PropertyContext context;

            if (index == -1)
            {
                context = CreateContext(
                    propertySymbol,
                    genericParameterMap,
                    globalTypeBuilder,
                    hasEventMembers,
                    contexts.Count
                );

                contexts.Add(context);
            }
            else
            {
                context = contexts[index];
                var created = CreateContext(
                    propertySymbol,
                    genericParameterMap,
                    globalTypeBuilder,
                    hasEventMembers,
                    index
                );

                var existingModel = context.Model;
                var createdModel = created.Model;

                context.Model = existingModel with
                {
                    GetterDelegateType = existingModel.GetterDelegateType ?? createdModel.GetterDelegateType,
                    SetterDelegateType = existingModel.SetterDelegateType ?? createdModel.SetterDelegateType,
                    GetterName = existingModel.GetterName ?? createdModel.GetterName,
                    SetterName = existingModel.SetterName ?? createdModel.SetterName,
                    GetterParameterName = existingModel.GetterParameterName ?? createdModel.GetterParameterName,
                    SetterParameterName = existingModel.SetterParameterName ?? createdModel.SetterParameterName,
                    GetterFieldName = existingModel.GetterFieldName ?? createdModel.GetterFieldName,
                    SetterFieldName = existingModel.SetterFieldName ?? createdModel.SetterFieldName,
                };
            }

            implementationContexts.Add(context);
        }

        var modelBuilder = ImmutableArray.CreateBuilder<PropertyGenerationModel>();

        foreach (var pair in builder)
        {
            foreach (var context in pair.Value)
            {
                context.ModelIndex = modelBuilder.Count;
                modelBuilder.Add(context.Model);
            }
        }

        var indexBuilder = ImmutableArray.CreateBuilder<int>(implementationContexts.Count);

        foreach (var context in implementationContexts)
        {
            indexBuilder.Add(context.ModelIndex);
        }

        return new ProviderCache(
            Models: modelBuilder.ToImmutable(),
            GenerationModelIndicesByImplementation: indexBuilder.ToImmutable()
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

            setterDelegateType = $"global::System.Action<{string.Join(", ", setterDelegateParameterTypes)}>";
        }
        else
        {
            setterDelegateType = null;
            setterName = null;
            setterParameterName = null;
            setterFieldName = null;
        }

        return new PropertyContext(
            propertySymbol.Type,
            propertySymbol.Parameters,
            new PropertyGenerationModel(
                IsIndexer: isIndexer,
                Type: propertyType,
                Name: propertySymbol.Name,
                ApiName: apiName,
                Parameters: parameterList,
                Arguments: argumentList,
                GetterDelegateType: getterDelegateType,
                SetterDelegateType: setterDelegateType,
                GetterName: getterName,
                SetterName: setterName,
                GetterParameterName: getterParameterName,
                SetterParameterName: setterParameterName,
                GetterFieldName: getterFieldName,
                SetterFieldName: setterFieldName,
                HasParameters: propertySymbol.Parameters.Length > 0
            )
        );
    }
    #endregion

    #region Fields
    private readonly ProviderCache _cache = CreateCache(
        propertySymbols,
        genericParameterMap,
        globalTypeBuilder,
        hasEventMembers
    );
    #endregion

    #region Properties
    public ImmutableArray<PropertyGenerationModel> Models => _cache.Models;
    public ImmutableArray<int> GenerationModelIndicesByImplementation =>
        _cache.GenerationModelIndicesByImplementation;
    #endregion
}
