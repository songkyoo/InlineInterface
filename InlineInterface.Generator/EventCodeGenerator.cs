using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;

namespace Macaron.InlineInterface;

public sealed class EventCodeGenerator(
    EventContextProvider eventContextProvider,
    InterfaceTypeStringProvider interfaceTypeStringProvider,
    ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
    string indent
)
{
    public IEnumerable<string> GetEventCollectionFieldDeclarations()
    {
        foreach (var context in eventContextProvider.Contexts)
        {
            yield return $"public {context.Type} {context.UniqueName};";
        }
    }

    public IEnumerable<ImmutableArray<string>> GetEventDispatcherImplementations()
    {
        foreach (var context in eventContextProvider.Contexts)
        {
            var methodSymbol = context.TypeSymbol.DelegateInvokeMethod;

            if (methodSymbol is null)
            {
                continue;
            }

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
            implementationBuilder.Add("{");
            implementationBuilder.Add($"{indent}if (_eventCollection.{context.UniqueName} == null) return{(methodSymbol.ReturnsVoid ? "" : " default")};");
            implementationBuilder.Add($"{indent}{(methodSymbol.ReturnsVoid ? "" : "return ")}_eventCollection.{context.UniqueName}({string.Join(", ", arguments)});");
            implementationBuilder.Add("}");

            yield return implementationBuilder.ToImmutable();
        }
    }

    public ImmutableArray<string> GetInterfaceImplementation(IEventSymbol eventSymbol)
    {
        if (!eventContextProvider.TryGetEventContext(eventSymbol, out var context))
        {
            return ImmutableArray<string>.Empty;
        }

        var interfaceTypeString = interfaceTypeStringProvider.GetInterfaceTypeName(eventSymbol.ContainingType);
        var implementationBuilder = ImmutableArray.CreateBuilder<string>();

        implementationBuilder.Add($"event {context.Type} {interfaceTypeString}.{context.Name}");
        implementationBuilder.Add("{");
        implementationBuilder.Add($"{indent}add => _eventCollection.{context.UniqueName} += value;");
        implementationBuilder.Add($"{indent}remove => _eventCollection.{context.UniqueName} -= value;");
        implementationBuilder.Add("}");

        return implementationBuilder.ToImmutable();
    }
}
