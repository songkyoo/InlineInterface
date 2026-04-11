using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.InlineInterface;

public sealed class MethodContextProvider(InterfaceTypeStringProvider interfaceTypeStringProvider, bool hasEventMembers)
{
    #region Static Methods
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
    private readonly Dictionary<string, List<MethodContext>> _cache = new();
    #endregion

    #region Properties
    public IEnumerable<MethodContext> Contexts => _cache.Values.SelectMany(x => x);
    #endregion

    #region Methods
    public string GetMethodImplementation(IMethodSymbol methodSymbol)
    {
        var context = GetMethodContext(methodSymbol);
        var interfaceTypeString = interfaceTypeStringProvider.GetInterfaceTypeName(methodSymbol.ContainingType);

        return $"{context.ReturnType} {interfaceTypeString}.{context.Name}({context.Parameters}) => ({context.FieldName} ?? throw new global::System.NotImplementedException())({context.Arguments});";
    }

    private MethodContext GetMethodContext(IMethodSymbol methodSymbol)
    {
        var methodName = methodSymbol.Name;

        if (!_cache.TryGetValue(methodName, out var contexts))
        {
            contexts = [];
            _cache.Add(methodSymbol.Name, contexts);
        }

        foreach (var context in contexts)
        {
            if (MatchesMethodSignature(methodSymbol, context.ReturnTypeSymbol, context.ParameterTypeSymbols))
            {
                return context;
            }
        }

        var uniqueName = $"Method_{methodName}_{contexts.Count}";
        var parameterName = $"method_{methodName}_{contexts.Count}";
        var fieldName = $"_{parameterName}";

        var paramTypes = new List<string>();
        var parameters = new List<string>();
        var arguments = new List<string>();

        if (hasEventMembers)
        {
            paramTypes.Add("EventCollection");
            arguments.Add("_eventCollection");
        }

        foreach (var paramSymbol in methodSymbol.Parameters)
        {
            var (type, name) = GetParameterString(paramSymbol);

            paramTypes.Add(type);
            parameters.Add($"{type} {name}");
            arguments.Add(name);
        };

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
            returnType = methodSymbol.ReturnType.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
                IncludeNullableReferenceTypeModifier |
                UseSpecialTypes
            ));
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

        return newContext;
    }
    #endregion
}
