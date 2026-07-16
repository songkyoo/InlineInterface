using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

public sealed class MethodContextProvider(
    ImmutableArray<IMethodSymbol> methodSymbols,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
    string globalTypeBuilder,
    bool hasEventMembers
)
{
    #region Types
    private sealed record ProviderCache(
        ImmutableArray<MethodGenerationModel> Models,
        ImmutableArray<int> GenerationModelIndicesByImplementation
    );
    #endregion

    #region Static Methods
    private static ProviderCache CreateCache(
        ImmutableArray<IMethodSymbol> methodSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string globalTypeBuilder,
        bool hasEventMembers
    )
    {
        var builder = new SortedDictionary<string, List<MethodContext>>();
        var implementationContexts = new List<MethodContext>(capacity: methodSymbols.Length);

        foreach (var methodSymbol in methodSymbols)
        {
            var methodName = methodSymbol.Name;
            var signature = MethodSignature.Create(methodSymbol);

            if (!builder.TryGetValue(methodName, out var contexts))
            {
                contexts = [];
                builder.Add(methodSymbol.Name, contexts);
            }

            MethodContext? context = null;

            foreach (var existingContext in contexts)
            {
                if (MethodSignatureComparer.Instance.Equals(signature, existingContext.Signature))
                {
                    context = existingContext;

                    break;
                }
            }

            if (context is null)
            {
                var uniqueName = $"Method_{methodName}_{contexts.Count}";
                var parameterName = $"method_{methodName}_{contexts.Count}";
                var fieldName = $"_{parameterName}";

                var eventParameterCount = hasEventMembers ? 1 : 0;
                var paramTypes = new List<string>(capacity: methodSymbol.Parameters.Length + eventParameterCount);
                var parameters = new List<string>(capacity: methodSymbol.Parameters.Length);
                var arguments = new List<string>(capacity: methodSymbol.Parameters.Length + eventParameterCount);

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

                context = new MethodContext(
                    signature,
                    model: new MethodGenerationModel(
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

                contexts.Add(context);
            }

            implementationContexts.Add(context);
        }

        var modelBuilder = ImmutableArray.CreateBuilder<MethodGenerationModel>(initialCapacity: methodSymbols.Length);

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
            indexBuilder.Add(context.ModelIndex);
        }

        return new ProviderCache(
            Models: modelBuilder.ToImmutable(),
            GenerationModelIndicesByImplementation: indexBuilder.ToImmutable()
        );
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

    public ImmutableArray<int> GenerationModelIndicesByImplementation => _cache.GenerationModelIndicesByImplementation;
    #endregion
}
