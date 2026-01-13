using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using static System.Linq.Enumerable;
using static Macaron.InlineInterface.SymbolHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

internal static class SourceGenerationHelpers
{
    const string Indent = "    ";

    public static void AddSource(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        ImmutableArray<IMethodSymbol> methodSymbols
    )
    {
        var (
            type,
            genericParameters,
            genericParameterConstraints
        ) = GetTypeStrings(typeSymbol);
        var methodContexts = CreateMethodContexts(methodSymbols);

        var stringBuilder = CreateStringBuilderWithFileHeader();
        var depthSpacerText = "";
        var typeBuilderNamespace = $"Macaron.InlineInterface.Implementations{GetNamespaceString(typeSymbol)}";

        // get nested types
        var nestedTypeNames = new List<string> { GetTypeName(typeSymbol) };
        var containingType = GetContainingType(typeSymbol);

        while (containingType != null)
        {
            nestedTypeNames.Add(GetTypeName(containingType));
            containingType = GetContainingType(containingType);
        }

        nestedTypeNames.Reverse();

        var mergedTypePrefix = string.Join("_", nestedTypeNames);
        var typeBuilder = $"{mergedTypePrefix}Builder{genericParameters}";

        // begin builder namespace
        stringBuilder.AppendLine($"namespace {typeBuilderNamespace}");
        stringBuilder.AppendLine($"{{");

        depthSpacerText += Indent;

        // begin builder type
        stringBuilder.Append($"{depthSpacerText}internal readonly record struct {typeBuilder}(");

        // builder constructor parameters
        for (var i = 0; i < methodContexts.Length; i++)
        {
            var methodContext = methodContexts[i];

            stringBuilder.AppendLine();
            stringBuilder.Append($"{depthSpacerText}{Indent}{methodContext.DelegateType}? {methodContext.UniqueName} = null");

            if (i < methodContexts.Length - 1)
            {
                stringBuilder.Append(",");
            }
        }

        stringBuilder.Append($")");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        // begin impl type
        stringBuilder.AppendLine($"{depthSpacerText}private sealed class Impl : {type}");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}readonly {methodContext.DelegateType} {methodContext.FieldName};");
        }

        stringBuilder.AppendLine();

        // impl constructor parameters
        stringBuilder.Append($"{depthSpacerText}public Impl(");

        for (var i = 0; i < methodContexts.Length; i++)
        {
            var methodContext = methodContexts[i];

            stringBuilder.AppendLine();
            stringBuilder.Append($"{depthSpacerText}{Indent}{methodContext.DelegateType} {methodContext.ParameterName}");

            if (i < methodContexts.Length - 1)
            {
                stringBuilder.Append(",");
            }
        }

        stringBuilder.Append($")");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        // begin impl constructor body
        depthSpacerText += Indent;

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{methodContext.FieldName} = {methodContext.ParameterName};");
        }

        // end impl constructor body
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // impl method implementations
        for (var i = 0; i < methodContexts.Length; i++)
        {
            var methodContext = methodContexts[i];

            stringBuilder.AppendLine($"{depthSpacerText}{methodContext.Implementation}");

            if (i < methodContexts.Length - 1)
            {
                stringBuilder.AppendLine();
            }
        }

        // end impl type
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // begin build method
        stringBuilder.AppendLine($"{depthSpacerText}public static {type} Build({typeBuilder} builder)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        stringBuilder.Append($"{depthSpacerText}return new Impl(");

        for (var i = 0; i < methodContexts.Length; i++)
        {
            var methodContext = methodContexts[i];

            stringBuilder.AppendLine();
            stringBuilder.Append($"{depthSpacerText}{Indent}{methodContext.ParameterName}: builder.{methodContext.UniqueName} ?? throw new global::System.InvalidOperationException()");

            if (i < methodContexts.Length - 1)
            {
                stringBuilder.Append(",");
            }
        }

        stringBuilder.Append($");");
        stringBuilder.AppendLine();

        // end build method
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // begin builder methods
        for (var i = 0; i < methodContexts.Length; i++)
        {
            var methodContext = methodContexts[i];

            stringBuilder.AppendLine($"{depthSpacerText}public {typeBuilder} {methodContext.Name}({methodContext.DelegateType} impl) => this with {{ {methodContext.UniqueName} = impl }};");

            if (i < methodContexts.Length - 1)
            {
                stringBuilder.AppendLine();
            }
        }

        // end builder methods

        // end builder type
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // end builder namespace
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // begin extension namespace
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"namespace Macaron.InlineInterface");
        stringBuilder.AppendLine($"{{");

        depthSpacerText += Indent;

        // begin extension class
        stringBuilder.AppendLine($"{depthSpacerText}internal static partial class ImplementationOfExtensions");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        // extension method
        var globalTypeBuilder = $"global::{typeBuilderNamespace}.{typeBuilder}";

        stringBuilder.AppendLine($"{depthSpacerText}public static {type} Create{genericParameters}(");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}this ImplementationOf<{type}> implementationOf,");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}global::System.Func<{globalTypeBuilder}, {globalTypeBuilder}> configure)");

        foreach (var constraint in genericParameterConstraints)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}{constraint}");
        }

        // begin method body
        stringBuilder.AppendLine($"{depthSpacerText}{{");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return {globalTypeBuilder}.Build(configure(new {globalTypeBuilder}()));");
        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // end extension class
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // end extension namespace
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"}}");

        context.AddSource(
            hintName: GetHintName(typeSymbol),
            sourceText: SourceText.From(stringBuilder.ToString(), Encoding.UTF8)
        );

        #region Local Functions
        static string GetNamespaceString(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? $".{ns.ToDisplayString()}" : "";
        }

        static INamedTypeSymbol? GetContainingType(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.ContainingType?.ConstructedFrom ?? typeSymbol.ContainingType;
        }

        static string GetTypeName(INamedTypeSymbol typeSymbol)
        {
            return $"{typeSymbol.Name}{(typeSymbol.Arity > 0 ? $"_{typeSymbol.Arity}" : "")}";
        }
        #endregion
    }

    private static StringBuilder CreateStringBuilderWithFileHeader()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("// <auto-generated />");
        stringBuilder.AppendLine("#nullable enable");
        stringBuilder.AppendLine();

        return stringBuilder;
    }

    private static (
        string Type,
        string GenericParameters,
        ImmutableArray<string> GenericParameterConstraints
    ) GetTypeStrings(INamedTypeSymbol typeSymbol)
    {
        var typeSymbols = GetNestedTypeSymbols(typeSymbol);

        if (!HasDuplicatedTypeParameterName(typeSymbols))
        {
            var typeParameters = typeSymbols
                .SelectMany(static symbol => symbol.TypeParameters)
                .ToArray();

            var type = typeSymbol.ToDisplayString(FullyQualifiedFormat);
            var genericParameters = string.Join(
                ", ",
                typeParameters.Select(static symbol => symbol.Name)
            ) is { Length: > 0 } parameters ? $"<{parameters }>": "";
            var genericParameterConstraints = typeParameters
                .Select(static symbol => GetTypeParameterConstraintClause(symbol, static name => name))
                .Where(static constraint => constraint.Length > 0)
                .ToImmutableArray();

            return (
                Type: type,
                GenericParameters: genericParameters,
                GenericParameterConstraints: genericParameterConstraints
            );
        }
        else
        {
            var @namespace = typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
                ? containingNamespace.ToDisplayString()
                : "";
            var types = new List<string>();
            var genericParameterConstraints = ImmutableArray.CreateBuilder<string>();
            var typeParameterIndex = 0;

            foreach (var symbol in typeSymbols)
            {
                var builder = new StringBuilder(symbol.Name);

                if (symbol.Arity > 0)
                {
                    var mapper = new Dictionary<string, string>();

                    builder.Append("<");

                    for (int i = 0; i < symbol.Arity; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        var replacedTypeParameterName = $"T{typeParameterIndex + i}";
                        builder.Append(replacedTypeParameterName);
                        mapper.Add(symbol.TypeParameters[i].Name, replacedTypeParameterName);
                    }

                    builder.Append(">");

                    typeParameterIndex += symbol.Arity;

                    foreach (var typeParameterSymbol in symbol.TypeParameters)
                    {
                        var clause = GetTypeParameterConstraintClause(
                            typeParameterSymbol,
                            name => mapper[name]
                        );

                        if (clause.Length > 0)
                        {
                            genericParameterConstraints.Add(clause);
                        }
                    }
                }

                types.Add(builder.ToString());
            }

            var type = $"global::{(@namespace.Length > 0 ? $"{@namespace}." : "")}{string.Join(".", types)}";
            var genericParameters = typeParameterIndex > 0
                ? $"<{string.Join(", ", Range(0, typeParameterIndex).Select(static index => $"T{index}"))}>"
                : "";

            return (
                Type: type,
                GenericParameters: genericParameters,
                GenericParameterConstraints: genericParameterConstraints.ToImmutable()
            );
        }
    }

    private static string GetHintName(INamedTypeSymbol typeSymbol)
    {
        var assemblyName = typeSymbol.ContainingAssembly != null ? $"{typeSymbol.ContainingAssembly}," : "";
        var qualifiedName = typeSymbol.ToDisplayString(FullyQualifiedFormat);

        const uint fnvPrime = 16777619;
        const uint offsetBasis = 2166136261;

        var bytes = Encoding.UTF8.GetBytes($"{assemblyName}, {qualifiedName}");
        uint hash = offsetBasis;

        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return $"{typeSymbol.Name}_{typeSymbol.Arity}.{hash:x8}.g.cs";
    }
}
