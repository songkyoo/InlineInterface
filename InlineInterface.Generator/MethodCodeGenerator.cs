using System.Collections.Immutable;

namespace Macaron.InlineInterface;

public sealed class MethodCodeGenerator(
    ImmutableArray<MethodGenerationModel> methods,
    ImmutableArray<string> interfaceTypes,
    string containingBuilderType,
    string indent
)
{
    public IEnumerable<string> GetImplFieldDeclarations()
    {
        foreach (var methodContext in methods)
        {
            yield return $"private readonly {methodContext.DelegateType}? {methodContext.FieldName};";
        }
    }

    public IEnumerable<string> GetImplConstructorParameterFragments()
    {
        foreach (var methodContext in methods)
        {
            yield return $"{methodContext.DelegateType}? {methodContext.ParameterName}";
        }
    }

    public IEnumerable<string> GetImplConstructorAssignments()
    {
        foreach (var methodContext in methods)
        {
            yield return $"{methodContext.FieldName} = {methodContext.ParameterName};";
        }
    }

    public IEnumerable<string> GetBuilderFieldDeclarations()
    {
        foreach (var methodContext in methods)
        {
            yield return $"private readonly {methodContext.DelegateType}? {methodContext.UniqueName} {{ get; init; }} = null;";
        }
    }

    public IEnumerable<string> GetBuilderConstructorParameterFragments()
    {
        foreach (var methodContext in methods)
        {
            yield return $"{methodContext.DelegateType}? {methodContext.ParameterName} = null";
        }
    }

    public IEnumerable<string> GetBuilderConstructorAssignments()
    {
        foreach (var methodContext in methods)
        {
            yield return $"{methodContext.UniqueName} = {methodContext.ParameterName};";
        }
    }

    public IEnumerable<string> GetBuilderMethodImplementation(string typeBuilder)
    {
        foreach (var context in methods)
        {
            yield return $"public {typeBuilder} {context.Name}({context.DelegateType} impl) => this with {{ {context.UniqueName} = impl }};";
        }
    }

    public IEnumerable<string> GetBuildArgumentFragments()
    {
        foreach (var methodContext in methods)
        {
            yield return $"{methodContext.ParameterName}: {methodContext.UniqueName} ?? (_allowMissingImplementation ? null : throw CreateMissingBuildDelegateException({CreateMessageLiteral($"method '{methodContext.Name}({methodContext.Parameters})'")}))";
        }
    }

    public string GetInterfaceImplementation(MethodImplementationModel implementation)
    {
        var context = methods[implementation.MethodIndex];
        var interfaceType = interfaceTypes[implementation.InterfaceTypeIndex];
        var memberDescription = CreateMessageLiteral($"method '{interfaceType}.{context.Name}({context.Parameters})'");

        return $"{context.ReturnType} {interfaceType}.{context.Name}({context.Parameters}) => ({context.FieldName} ?? throw {containingBuilderType}.CreateMissingInvocationDelegateException({memberDescription}))({context.Arguments});";
    }

    public IEnumerable<ImmutableArray<string>> GetExtensionMethodImplementation(
        string type,
        string globalTypeBuilder,
        string genericParameters,
        ImmutableArray<string> genericParameterConstraints
    )
    {
        foreach (var context in methods)
        {
            var implementationBuilder = ImmutableArray.CreateBuilder<string>();

            implementationBuilder.Add($"public static {globalTypeBuilder} {context.Name}{genericParameters}(");
            implementationBuilder.Add($"{indent}this global::Macaron.InlineInterface.ImplementationOf<{type}> implementationOf,");
            implementationBuilder.Add($"{indent}{context.DelegateType} impl)");

            foreach (var constraint in genericParameterConstraints)
            {
                implementationBuilder.Add($"{indent}{constraint}");
            }

            implementationBuilder.Add("{");
            implementationBuilder.Add($"{indent}return new {globalTypeBuilder}(allowMissingImplementation: implementationOf.AllowMissingImplementation, {context.ParameterName}: impl);");
            implementationBuilder.Add("}");

            yield return implementationBuilder.ToImmutable();
        }
    }

    private static string CreateMessageLiteral(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
