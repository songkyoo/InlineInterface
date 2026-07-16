using System.Collections.Immutable;

namespace Macaron.InlineInterface;

public sealed class PropertyCodeGenerator(
    ImmutableArray<PropertyGenerationModel> properties,
    ImmutableArray<string> interfaceTypes,
    string containingBuilderType,
    string indent
)
{
    public IEnumerable<string> GetImplFieldDeclarations()
    {
        foreach (var propertyContext in properties)
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
        foreach (var propertyContext in properties)
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
        foreach (var propertyContext in properties)
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
        foreach (var propertyContext in properties)
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
        foreach (var propertyContext in properties)
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
        foreach (var propertyContext in properties)
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
        foreach (var context in properties)
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
        foreach (var propertyContext in properties)
        {
            if (propertyContext is
            {
                GetterName: { } getterName,
                GetterParameterName: { } getterParameterName,
            })
            {
                yield return $"{getterParameterName}: {getterName} ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException({CreateBuildMemberDescriptionLiteral(propertyContext, "getter")}))";
            }

            if (propertyContext is
            {
                SetterName: { } setterName,
                SetterParameterName: { } setterParameterName,
            })
            {
                yield return $"{setterParameterName}: {setterName} ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException({CreateBuildMemberDescriptionLiteral(propertyContext, "setter")}))";
            }
        }
    }

    public ImmutableArray<string> GetInterfaceImplementation(PropertyImplementationModel implementation)
    {
        var context = properties[implementation.PropertyIndex];
        var interfaceType = interfaceTypes[implementation.InterfaceTypeIndex];
        var implementationBuilder = ImmutableArray.CreateBuilder<string>();

        var propertyName = context.IsIndexer ? "this" : context.Name;
        var parameterList = context.HasParameters
            ? $"[{context.Parameters}]"
            : "";

        implementationBuilder.Add($"{context.Type} {interfaceType}.{propertyName}{parameterList}");
        implementationBuilder.Add("{");

        if (implementation.HasGetter)
        {
            var getterArguments = context.Arguments;
            var getterMemberDescription = CreateInvocationMemberDescriptionLiteral(
                interfaceType,
                context,
                "getter"
            );

            implementationBuilder.Add($"{indent}get => ({context.GetterFieldName} ?? throw {containingBuilderType}.CreateMissingInvocationDelegateException({getterMemberDescription}))({getterArguments});");
        }

        if (implementation.HasSetter)
        {
            var setterArguments = string.IsNullOrEmpty(context.Arguments)
                ? "value"
                : $"{context.Arguments}, value";
            var setterMemberDescription = CreateInvocationMemberDescriptionLiteral(
                interfaceType,
                context,
                "setter"
            );

            implementationBuilder.Add($"{indent}set => ({context.SetterFieldName} ?? throw {containingBuilderType}.CreateMissingInvocationDelegateException({setterMemberDescription}))({setterArguments});");
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
        foreach (var context in properties)
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

    private static string CreateBuildMemberDescriptionLiteral(PropertyGenerationModel context, string accessor)
    {
        var memberDescription = context.IsIndexer
            ? $"indexer '{CreateIndexerSignature(context.Parameters)}' ({accessor})"
            : $"property '{context.Name}' ({accessor})";

        return ToStringLiteral(memberDescription);
    }

    private static string CreateInvocationMemberDescriptionLiteral(
        string interfaceTypeString,
        PropertyGenerationModel context,
        string accessor
    )
    {
        var memberDescription = context.IsIndexer
            ? $"indexer '{interfaceTypeString}.this[{context.Parameters}]' ({accessor})"
            : $"property '{interfaceTypeString}.{context.Name}' ({accessor})";

        return ToStringLiteral(memberDescription);
    }

    private static string CreateIndexerSignature(string parameters)
    {
        return $"this[{parameters}]";
    }

    private static string ToStringLiteral(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
