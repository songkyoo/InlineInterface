using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

internal sealed class MethodContextProvider(
    IEnumerable<IMethodSymbol> methodSymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
    string globalTypeBuilder,
    bool hasEventMembers
)
{
    #region Nested Types
    private sealed record ProviderCache(
        ImmutableSortedDictionary<string, ImmutableArray<MethodContext>> Contexts,
        ImmutableArray<MethodGenerationModel> Models
    );
    #endregion

    #region Static Methods
    private static ProviderCache CreateCache(
        IEnumerable<IMethodSymbol> methodSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string globalTypeBuilder,
        bool hasEventMembers
    )
    {
        var builder = new SortedDictionary<string, List<MethodContext>>();

        foreach (var methodSymbol in methodSymbols)
        {
            var methodName = methodSymbol.Name;

            if (!builder.TryGetValue(methodName, out var contexts))
            {
                contexts = [];
                builder.Add(methodSymbol.Name, contexts);
            }

            if (contexts.Any(x => MatchesMethodSignature(methodSymbol, x.ReturnTypeSymbol, x.ParameterTypeSymbols)))
            {
                continue;
            }

            var uniqueName = $"Method_{methodName}_{contexts.Count}";
            var parameterName = $"method_{methodName}_{contexts.Count}";
            var fieldName = $"_{parameterName}";

            var paramTypes = new List<string>();
            var parameters = new List<string>();
            var arguments = new List<string>();

            if (hasEventMembers)
            {
                paramTypes.Add($"{globalTypeBuilder}.EventDispatcher");
                arguments.Add("_eventDispatcher");
            }

            foreach (var paramSymbol in methodSymbol.Parameters)
            {
                var (type, name) = GetParameterString(paramSymbol, genericParameterMap);

                paramTypes.Add(type);
                parameters.Add($"{type} {name}");
                arguments.Add(name);
            }

            var paramTypeList = string.Join(", ", paramTypes);

            string returnType;
            string delegateType;

            if (methodSymbol.ReturnsVoid)
            {
                returnType = "void";
                delegateType = paramTypeList.Length > 0
                    ? $"global::System.Action<{string.Join(", ", paramTypeList)}>"
                    : $"global::System.Action";
            }
            else
            {
                returnType = SymbolHelpers.GetTypeString(methodSymbol.ReturnType, genericParameterMap);
                delegateType = paramTypeList.Length > 0
                    ? $"global::System.Func<{paramTypeList}, {returnType}>"
                    : $"global::System.Func<{returnType}>";
            }

            var newContext = new MethodContext(
                ReturnTypeSymbol: methodSymbol.ReturnType,
                ParameterTypeSymbols: methodSymbol.Parameters,
                Model: new MethodGenerationModel(
                    ReturnType: returnType,
                    Parameters: string.Join(", ", parameters),
                    Arguments: string.Join(", ", arguments),
                    DelegateType: delegateType,
                    Name: methodName,
                    UniqueName: uniqueName,
                    ParameterName: parameterName,
                    FieldName: fieldName
                )
            );

            contexts.Add(newContext);
        }

        var contextCacheBuilder = ImmutableSortedDictionary.CreateBuilder<string, ImmutableArray<MethodContext>>();
        var modelBuilder = ImmutableArray.CreateBuilder<MethodGenerationModel>();

        foreach (var pair in builder)
        {
            var contexts = pair.Value;
            var contextBuilder = ImmutableArray.CreateBuilder<MethodContext>(contexts.Count);

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

    private static bool MatchesMethodSignature(
        IMethodSymbol methodSymbol,
        ITypeSymbol returnType,
        ImmutableArray<IParameterSymbol> parameterTypes
    )
    {
        var comparer = SymbolEqualityComparer.Default;

        if (!comparer.Equals(returnType, methodSymbol.ReturnType))
        {
            return false;
        }

        if (parameterTypes.Length != methodSymbol.Parameters.Length)
        {
            return false;
        }

        for (var i = 0; i < parameterTypes.Length; i++)
        {
            var paramSymbol = parameterTypes[i];
            var targetParamSymbol = methodSymbol.Parameters[i];

            if (!comparer.Equals(paramSymbol.Type, targetParamSymbol.Type))
            {
                return false;
            }

            if (paramSymbol.RefKind != targetParamSymbol.RefKind)
            {
                return false;
            }

            if (paramSymbol.IsParams != targetParamSymbol.IsParams)
            {
                return false;
            }
        }

        return true;
    }
    #endregion

    #region Fields
    private readonly ProviderCache _cache = CreateCache(
        methodSymbols,
        genericParameterMap,
        globalTypeBuilder,
        hasEventMembers
    );
    #endregion

    #region Properties
    public ImmutableArray<MethodGenerationModel> Models => _cache.Models;
    #endregion

    #region Methods
    public bool TryGetMethodModelIndex(
        IMethodSymbol methodSymbol,
        out int modelIndex
    )
    {
        if (!_cache.Contexts.TryGetValue(methodSymbol.Name, out var contexts))
        {
            modelIndex = -1;

            return false;
        }

        var index = -1;

        for (var i = 0; i < contexts.Length; i++)
        {
            if (MatchesMethodSignature(methodSymbol, contexts[i].ReturnTypeSymbol, contexts[i].ParameterTypeSymbols))
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
