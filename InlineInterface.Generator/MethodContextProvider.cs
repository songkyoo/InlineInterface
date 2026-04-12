using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

public sealed class MethodContextProvider(
    IEnumerable<IMethodSymbol> methodSymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
    InterfaceTypeStringProvider interfaceTypeStringProvider,
    string globalTypeBuilder,
    bool hasEventMembers
)
{
    #region Static Methods
    private static ImmutableSortedDictionary<string, ImmutableArray<MethodContext>> CreateCache(
        IEnumerable<IMethodSymbol> methodSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string globalTypeBuilder,
        bool hasEventMembers
    )
    {
        var builder = new Dictionary<string, List<MethodContext>>();

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
                ReturnType: returnType,
                Parameters: string.Join(", ", parameters),
                Arguments: string.Join(", ", arguments),
                DelegateType: delegateType,
                Name: methodName,
                UniqueName: uniqueName,
                ParameterName: parameterName,
                FieldName: fieldName
            );

            contexts.Add(newContext);
        }

        return builder.ToImmutableSortedDictionary(
            keySelector: x => x.Key,
            elementSelector: x => x.Value.ToImmutableArray()
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
    private readonly ImmutableSortedDictionary<string, ImmutableArray<MethodContext>> _cache = CreateCache(
        methodSymbols,
        genericParameterMap,
        globalTypeBuilder,
        hasEventMembers
    );
    #endregion

    #region Properties
    public IEnumerable<MethodContext> Contexts => _cache.Values.SelectMany(x => x);
    #endregion

    #region Methods
    public string GetInterfaceImplementation(IMethodSymbol methodSymbol)
    {
        if (!TryGetMethodContext(methodSymbol, out var context))
        {
            return "";
        }

        var interfaceTypeString = interfaceTypeStringProvider.GetInterfaceTypeName(methodSymbol.ContainingType);

        return $"{context.ReturnType} {interfaceTypeString}.{context.Name}({context.Parameters}) => ({context.FieldName} ?? throw new global::System.NotImplementedException())({context.Arguments});";
    }

    private bool TryGetMethodContext(
        IMethodSymbol methodSymbol,
        [NotNullWhen(returnValue: true)]out MethodContext? context
    )
    {
        if (!_cache.TryGetValue(methodSymbol.Name, out var contexts))
        {
            context = null;

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
            context = null;

            return false;
        }

        context = contexts[index];

        return true;
    }
    #endregion
}
