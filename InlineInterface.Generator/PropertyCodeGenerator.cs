using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed class PropertyCodeGenerator(
    PropertyContextProvider propertyContextProvider,
    InterfaceTypeStringProvider interfaceTypeStringProvider,
    string indent
)
{
    public IEnumerable<string> GetImplFieldDeclarations()
    {
        foreach (var propertyContext in propertyContextProvider.Contexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterFieldName: { } getterFieldName,
            })
            {
                yield return $"private readonly {getterDelegateType}? {getterFieldName};";
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterFieldName: { } setterFieldName,
            })
            {
                yield return $"private readonly {setterDelegateType}? {setterFieldName};";
            }
        }
    }

    public IEnumerable<string> GetImplConstructorParameterFragments()
    {
        foreach (var propertyContext in propertyContextProvider.Contexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterParameterName: { } getterParameterName,
            })
            {
                yield return $"{getterDelegateType}? {getterParameterName}";
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterParameterName: { } setterParameterName,
            })
            {
                yield return $"{setterDelegateType}? {setterParameterName}";
            }
        }
    }

    public IEnumerable<string> GetImplConstructorAssignments()
    {
        foreach (var propertyContext in propertyContextProvider.Contexts)
        {
            if (propertyContext is
            {
                GetterFieldName: { } getterFieldName,
                GetterParameterName: { } getterParameterName,
            })
            {
                yield return $"{getterFieldName} = {getterParameterName};";
            }

            if (propertyContext is
            {
                SetterFieldName: { } setterFieldName,
                SetterParameterName: { } setterParameterName,
            })
            {
                yield return $"{setterFieldName} = {setterParameterName};";
            }
        }
    }

    public IEnumerable<string> GetBuilderFieldDeclarations()
    {
        foreach (var propertyContext in propertyContextProvider.Contexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterName: { } getterName,
            })
            {
                yield return $"private readonly {getterDelegateType}? {getterName} {{ get; init; }} = null;";
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterName: { } setterName,
            })
            {
                yield return $"private readonly {setterDelegateType}? {setterName} {{ get; init; }} = null;";
            }
        }
    }

    public IEnumerable<string> GetBuilderConstructorParameterFragments()
    {
        foreach (var propertyContext in propertyContextProvider.Contexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterParameterName: { } getterParameterName,
            })
            {
                yield return $"{getterDelegateType}? {getterParameterName} = null";
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterParameterName: { } setterParameterName,
            })
            {
                yield return $"{setterDelegateType}? {setterParameterName} = null";
            }
        }
    }

    public IEnumerable<string> GetBuilderConstructorAssignments()
    {
        foreach (var propertyContext in propertyContextProvider.Contexts)
        {
            if (propertyContext is
            {
                GetterName: { } getterName,
                GetterParameterName: { } getterParameterName,
            })
            {
                yield return $"{getterName} = {getterParameterName};";
            }

            if (propertyContext is
            {
                SetterName: { } setterName,
                SetterParameterName: { } setterParameterName,
            })
            {
                yield return $"{setterName} = {setterParameterName};";
            }
        }
    }

    public IEnumerable<string> GetBuilderMethodImplementation(string typeBuilder)
    {
        foreach (var context in propertyContextProvider.Contexts)
        {
            var parameters = new List<string>();
            var expressions = new List<string>();

            if (context is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterName: { } getterName,
            })
            {
                parameters.Add($"{getterDelegateType} getter");
                expressions.Add($"{getterName} = getter");
            }

            if (context is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterName: { } setterName,
            })
            {
                parameters.Add($"{setterDelegateType} setter");
                expressions.Add($"{setterName} = setter");
            }

            yield return $"public {typeBuilder} {context.ApiName}({string.Join(", ", parameters)}) => this with {{ {string.Join(", ", expressions)} }};";
        }
    }

    public IEnumerable<string> GetBuildArgumentFragments()
    {
        foreach (var propertyContext in propertyContextProvider.Contexts)
        {
            if (propertyContext is
            {
                GetterName: { } getterName,
                GetterParameterName: { } getterParameterName,
            })
            {
                yield return $"{getterParameterName}: {getterName} ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())";
            }

            if (propertyContext is
            {
                SetterName: { } setterName,
                SetterParameterName: { } setterParameterName,
            })
            {
                yield return $"{setterParameterName}: {setterName} ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())";
            }
        }
    }

    public ImmutableArray<string> GetInterfaceImplementation(IPropertySymbol propertySymbol)
    {
        if (!propertyContextProvider.TryGetPropertyContext(propertySymbol, out var context))
        {
            return ImmutableArray<string>.Empty;
        }

        var interfaceTypeString = interfaceTypeStringProvider.GetInterfaceTypeName(propertySymbol.ContainingType);
        var implementationBuilder = ImmutableArray.CreateBuilder<string>();

        var propertyName = context.IsIndexer ? "this" : context.Name;
        var parameterList = context.ParameterSymbols.Length > 0
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

    public IEnumerable<ImmutableArray<string>> GetExtensionMethodImplementation(
        string type,
        string globalTypeBuilder,
        string genericParameters,
        ImmutableArray<string> genericParameterConstraints
    )
    {
        foreach (var context in propertyContextProvider.Contexts)
        {
            var parameters = new List<string>();
            var expressions = new List<string>();
            var implementationBuilder = ImmutableArray.CreateBuilder<string>();

            if (context is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterParameterName: { } getterParameterName,
            })
            {
                parameters.Add($"{getterDelegateType} getter");
                expressions.Add($"{getterParameterName}: getter");
            }

            if (context is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterParameterName: { } setterParameterName,
            })
            {
                parameters.Add($"{setterDelegateType} setter");
                expressions.Add($"{setterParameterName}: setter");
            }

            implementationBuilder.Add($"public static {globalTypeBuilder} {context.ApiName}{genericParameters}(");
            implementationBuilder.Add($"{indent}this global::Macaron.InlineInterface.ImplementationOf<{type}> implementationOf,");

            for (var i = 0; i < parameters.Count - 1; i++)
            {
                implementationBuilder.Add($"{indent}{parameters[i]},");
            }

            if (parameters.Count > 0)
            {
                implementationBuilder.Add($"{indent}{parameters.Last()})");
            }

            foreach (var constraint in genericParameterConstraints)
            {
                implementationBuilder.Add($"{indent}{constraint}");
            }

            implementationBuilder.Add("{");
            implementationBuilder.Add($"{indent}return new {globalTypeBuilder}(allowMissingImplementation: implementationOf.AllowMissingImplementation, {string.Join(", ", expressions)});");
            implementationBuilder.Add("}");

            yield return implementationBuilder.ToImmutable();
        }
    }
}
