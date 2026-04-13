using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public sealed class MethodCodeGenerator(
    MethodContextProvider methodContextProvider,
    InterfaceTypeStringProvider interfaceTypeStringProvider,
    string indent
)
{
    public IEnumerable<string> GetImplFieldDeclarations()
    {
        foreach (var methodContext in methodContextProvider.Contexts)
        {
            yield return $"private readonly {methodContext.DelegateType}? {methodContext.FieldName};";
        }
    }

    public IEnumerable<string> GetImplConstructorParameterFragments()
    {
        foreach (var methodContext in methodContextProvider.Contexts)
        {
            yield return $"{methodContext.DelegateType}? {methodContext.ParameterName}";
        }
    }

    public IEnumerable<string> GetImplConstructorAssignments()
    {
        foreach (var methodContext in methodContextProvider.Contexts)
        {
            yield return $"{methodContext.FieldName} = {methodContext.ParameterName};";
        }
    }

    public IEnumerable<string> GetBuilderFieldDeclarations()
    {
        foreach (var methodContext in methodContextProvider.Contexts)
        {
            yield return $"private readonly {methodContext.DelegateType}? {methodContext.UniqueName} {{ get; init; }} = null;";
        }
    }

    public IEnumerable<string> GetBuilderConstructorParameterFragments()
    {
        foreach (var methodContext in methodContextProvider.Contexts)
        {
            yield return $"{methodContext.DelegateType}? {methodContext.ParameterName} = null";
        }
    }

    public IEnumerable<string> GetBuilderConstructorAssignments()
    {
        foreach (var methodContext in methodContextProvider.Contexts)
        {
            yield return $"{methodContext.UniqueName} = {methodContext.ParameterName};";
        }
    }

    public IEnumerable<string> GetBuilderMethodImplementation(string typeBuilder)
    {
        foreach (var context in methodContextProvider.Contexts)
        {
            yield return $"public {typeBuilder} {context.Name}({context.DelegateType} impl) => this with {{ {context.UniqueName} = impl }};";
        }
    }

    public IEnumerable<string> GetBuildArgumentFragments()
    {
        foreach (var methodContext in methodContextProvider.Contexts)
        {
            yield return $"{methodContext.ParameterName}: {methodContext.UniqueName} ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())";
        }
    }

    public ImmutableArray<string> GetInterfaceImplementation(IMethodSymbol methodSymbol)
    {
        if (!methodContextProvider.TryGetMethodContext(methodSymbol, out var context))
        {
            return ImmutableArray<string>.Empty;
        }

        var interfaceTypeString = interfaceTypeStringProvider.GetInterfaceTypeName(methodSymbol.ContainingType);

        return ImmutableArray.Create(
            $"{context.ReturnType} {interfaceTypeString}.{context.Name}({context.Parameters}) => ({context.FieldName} ?? throw new global::System.NotImplementedException())({context.Arguments});"
        );
    }

    public IEnumerable<ImmutableArray<string>> GetExtensionMethodImplementation(
        string type,
        string globalTypeBuilder,
        string genericParameters,
        ImmutableArray<string> genericParameterConstraints
    )
    {
        foreach (var context in methodContextProvider.Contexts)
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
}
