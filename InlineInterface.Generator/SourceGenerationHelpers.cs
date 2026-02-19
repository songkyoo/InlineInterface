using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using static System.Linq.Enumerable;
using static Macaron.InlineInterface.SymbolHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.InlineInterface;

internal static class SourceGenerationHelpers
{
    const string Indent = "    ";

    public static void AddSource(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        ImmutableArray<IEventSymbol> eventSymbols,
        ImmutableArray<IPropertySymbol> propertySymbols,
        ImmutableArray<IMethodSymbol> methodSymbols
    )
    {
        var (
            type,
            genericParameters,
            genericParameterConstraints
        ) = GetTypeStrings(typeSymbol);
        var propertyContexts = CreatePropertyContexts(propertySymbols);
        var methodContexts = CreateMethodContexts(methodSymbols);

        var stringBuilder = CreateStringBuilderWithFileHeader();
        var depthSpacerText = "";
        var typeBuilderNamespace = $"Macaron.InlineInterface.Generated{GetNamespaceString(typeSymbol)}";

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
        stringBuilder.AppendLine($"{depthSpacerText}internal readonly struct {typeBuilder}");

        // constraints
        foreach (var constraint in genericParameterConstraints)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}{constraint}");
        }

        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        // begin impl type
        stringBuilder.AppendLine($"{depthSpacerText}private sealed class Impl : {type}");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterFieldName: { } getterFieldName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}private readonly {getterDelegateType} {getterFieldName};");
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterFieldName: { } setterFieldName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}private readonly {setterDelegateType} {setterFieldName};");
            }
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}private readonly {methodContext.DelegateType} {methodContext.FieldName};");
        }

        stringBuilder.AppendLine();

        // impl constructor parameters
        stringBuilder.Append($"{depthSpacerText}public Impl(");

        var implConstructorParams = new List<string>();

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterParameterName: { } getterParameterName,
            })
            {
                implConstructorParams.Add($"{getterDelegateType} {getterParameterName}");
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterParameterName: { } setterParameterName,
            })
            {
                implConstructorParams.Add($"{setterDelegateType} {setterParameterName}");
            }
        }

        foreach (var methodContext in methodContexts)
        {
            implConstructorParams.Add($"{methodContext.DelegateType} {methodContext.ParameterName}");
        }

        if (implConstructorParams.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.Append($"{depthSpacerText}{Indent}");
            stringBuilder.Append(string.Join($",{Environment.NewLine}{depthSpacerText}{Indent}", implConstructorParams));
        }

        stringBuilder.Append($")");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        // begin impl constructor body
        depthSpacerText += Indent;

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterFieldName: { } getterFieldName,
                GetterParameterName: { } getterParameterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}{getterFieldName} = {getterParameterName};");
            }

            if (propertyContext is
            {
                SetterFieldName: { } setterFieldName,
                SetterParameterName: { } setterParameterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}{setterFieldName} = {setterParameterName};");
            }
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{methodContext.FieldName} = {methodContext.ParameterName};");
        }

        // end impl constructor body
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // impl event implementations
        if (eventSymbols.Length > 0)
        {
            stringBuilder.AppendLine();
        }

        for (var i = 0; i < eventSymbols.Length; i++)
        {
            var eventSymbol = eventSymbols[i];
            var eventType = eventSymbol.Type.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
                IncludeNullableReferenceTypeModifier |
                UseSpecialTypes
            ));

            if (eventSymbol.NullableAnnotation != NullableAnnotation.Annotated)
            {
                eventType += "?";
            }

            stringBuilder.AppendLine($"{depthSpacerText}public event {eventType} {eventSymbol.Name};");

            if (i < eventSymbols.Length - 1)
            {
                stringBuilder.AppendLine();
            }
        }

        // impl property implementations
        if (propertyContexts.Length > 0)
        {
            stringBuilder.AppendLine();
        }

        for (var i = 0; i < propertyContexts.Length; i++)
        {
            var propertyContext = propertyContexts[i];

            foreach (var line in propertyContext.Implementation)
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }

            if (i < methodContexts.Length - 1)
            {
                stringBuilder.AppendLine();
            }
        }

        // impl method implementations
        if (methodContexts.Length > 0)
        {
            stringBuilder.AppendLine();
        }

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

        // builder field members
        stringBuilder.AppendLine($"{depthSpacerText}private readonly {type}? _base;");
        stringBuilder.AppendLine();

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterName: { } getterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}private readonly {getterDelegateType}? {getterName} {{ get; init; }} = null;");
                stringBuilder.AppendLine();
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterName: { } setterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}private readonly {setterDelegateType}? {setterName} {{ get; init; }} = null;");
                stringBuilder.AppendLine();
            }
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}private readonly {methodContext.DelegateType}? {methodContext.UniqueName} {{ get; init; }} = null;");
            stringBuilder.AppendLine();
        }

        // builder constructor parameters
        stringBuilder.Append($"{depthSpacerText}public {mergedTypePrefix}Builder(");
        stringBuilder.AppendLine();
        stringBuilder.Append($"{depthSpacerText}{Indent}{type}? @base");

        var builderConstructorParams = new List<string>();

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterParameterName: { } getterParameterName,
            })
            {
                builderConstructorParams.Add($"{getterDelegateType}? {getterParameterName} = null");
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterParameterName: { } setterParameterName,
            })
            {
                builderConstructorParams.Add($"{setterDelegateType}? {setterParameterName} = null");
            }
        }

        foreach (var methodContext in methodContexts)
        {
            builderConstructorParams.Add($"{methodContext.DelegateType}? {methodContext.ParameterName} = null");
        }

        if (builderConstructorParams.Count > 0)
        {
            stringBuilder.AppendLine(",");
            stringBuilder.Append($"{depthSpacerText}{Indent}");
            stringBuilder.Append(string.Join($",{Environment.NewLine}{depthSpacerText}{Indent}", builderConstructorParams));
        }

        stringBuilder.Append($")");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        // begin builder constructor body
        depthSpacerText += Indent;

        stringBuilder.AppendLine($"{depthSpacerText}_base = @base;");

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterName: { } getterName,
                GetterParameterName: { } getterParameterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}{getterName} = {getterParameterName};");
            }

            if (propertyContext is
            {
                SetterName: { } setterName,
                SetterParameterName: { } setterParameterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}{setterName} = {setterParameterName};");
            }
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{methodContext.UniqueName} = {methodContext.ParameterName};");
        }

        // end builder constructor body
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // builder methods
        foreach (var propertyContext in propertyContexts)
        {
            var parameters = new List<string>();
            var expressions = new List<string>();

            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterName: { } getterName,
            })
            {
                parameters.Add($"{getterDelegateType} getter");
                expressions.Add($"{getterName} = getter");
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterName: { } setterName,
            })
            {
                parameters.Add($"{setterDelegateType} setter");
                expressions.Add($"{setterName} = setter");
            }

            stringBuilder.AppendLine($"{depthSpacerText}public {typeBuilder} {propertyContext.Name}({string.Join(", ", parameters)}) => this with {{ {string.Join(", ", expressions)} }};");
            stringBuilder.AppendLine();
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}public {typeBuilder} {methodContext.Name}({methodContext.DelegateType} impl) => this with {{ {methodContext.UniqueName} = impl }};");
            stringBuilder.AppendLine();
        }

        // begin build method
        stringBuilder.AppendLine($"{depthSpacerText}public {type} Build(global::Macaron.InlineInterface.Tag _ = default)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterParameterName: { } getterParameterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}{getterDelegateType}? {getterParameterName} = null;");
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterParameterName: { } setterParameterName,
            })
            {
                stringBuilder.AppendLine($"{depthSpacerText}{setterDelegateType}? {setterParameterName} = null;");
            }
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{methodContext.DelegateType}? {methodContext.ParameterName} = null;");
        }

        if (propertyContexts.Length + methodContexts.Length > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"{depthSpacerText}if (_base != null)");
            stringBuilder.AppendLine($"{depthSpacerText}{{");

            foreach (var propertyContext in propertyContexts)
            {
                if (propertyContext is
                {
                    GetterParameterName: { } getterParameterName,
                })
                {
                    stringBuilder.AppendLine($"{depthSpacerText}{Indent}{getterParameterName} = () => _base.{propertyContext.Name};");
                }

                if (propertyContext is
                {
                    SetterParameterName: { } setterParameterName,
                })
                {
                    stringBuilder.AppendLine($"{depthSpacerText}{Indent}{setterParameterName} = value => _base.{propertyContext.Name} = value;");
                }
            }

            foreach (var methodContext in methodContexts)
            {
                stringBuilder.AppendLine($"{depthSpacerText}{Indent}{methodContext.ParameterName} = _base.{methodContext.Name};");
            }

            stringBuilder.AppendLine($"{depthSpacerText}}}");
            stringBuilder.AppendLine();
        }

        stringBuilder.Append($"{depthSpacerText}return new Impl(");

        var implConstructorArgs = new List<string>();

        foreach (var propertyContext in propertyContexts)
        {
            if (propertyContext is
            {
                GetterName: { } getterName,
                GetterParameterName: { } getterParameterName,
            })
            {
                implConstructorArgs.Add($"{getterParameterName}: {getterName} ?? {getterParameterName} ?? throw new global::System.InvalidOperationException()");
            }

            if (propertyContext is
            {
                SetterName: { } setterName,
                SetterParameterName: { } setterParameterName,
            })
            {
                implConstructorArgs.Add($"{setterParameterName}: {setterName} ?? {setterParameterName} ?? throw new global::System.InvalidOperationException()");
            }
        }

        foreach (var methodContext in methodContexts)
        {
            implConstructorArgs.Add($"{methodContext.ParameterName}: {methodContext.UniqueName} ?? {methodContext.ParameterName} ?? throw new global::System.InvalidOperationException()");
        }

        if (implConstructorArgs.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.Append($"{depthSpacerText}{Indent}");
            stringBuilder.Append(string.Join($",{Environment.NewLine}{depthSpacerText}{Indent}", implConstructorArgs));
        }

        stringBuilder.Append($");");
        stringBuilder.AppendLine();

        // end build method
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

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

        // extension methods
        var globalTypeBuilder = $"global::{typeBuilderNamespace}.{typeBuilder}";

        foreach (var propertyContext in propertyContexts)
        {
            var parameters = new List<string>();
            var expressions = new List<string>();

            if (propertyContext is
            {
                GetterDelegateType: { } getterDelegateType,
                GetterParameterName: { } getterParameterName,
            })
            {
                parameters.Add($"{getterDelegateType} getter");
                expressions.Add($"{getterParameterName}: getter");
            }

            if (propertyContext is
            {
                SetterDelegateType: { } setterDelegateType,
                SetterParameterName: { } setterParameterName,
            })
            {
                parameters.Add($"{setterDelegateType} setter");
                expressions.Add($"{setterParameterName}: setter");
            }

            stringBuilder.AppendLine($"{depthSpacerText}public static {globalTypeBuilder} {propertyContext.Name}{genericParameters}(");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}this global::Macaron.InlineInterface.ImplementationOf<{type}> implementationOf,");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}{string.Join($",{Environment.NewLine}{depthSpacerText}{Indent}", parameters)})");

            // constraints
            foreach (var constraint in genericParameterConstraints)
            {
                stringBuilder.AppendLine($"{depthSpacerText}{Indent}{constraint}");
            }

            // method body
            stringBuilder.AppendLine($"{depthSpacerText}{{");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new {globalTypeBuilder}(@base: implementationOf.Base, {string.Join(", ", expressions)});");
            stringBuilder.AppendLine($"{depthSpacerText}}}");
            stringBuilder.AppendLine();
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}public static {globalTypeBuilder} {methodContext.Name}{genericParameters}(");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}this global::Macaron.InlineInterface.ImplementationOf<{type}> implementationOf,");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}{methodContext.DelegateType} impl)");

            // constraints
            foreach (var constraint in genericParameterConstraints)
            {
                stringBuilder.AppendLine($"{depthSpacerText}{Indent}{constraint}");
            }

            // method body
            stringBuilder.AppendLine($"{depthSpacerText}{{");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new {globalTypeBuilder}(@base: implementationOf.Base, {methodContext.ParameterName}: impl);");
            stringBuilder.AppendLine($"{depthSpacerText}}}");
            stringBuilder.AppendLine();
        }

        // extension build method
        stringBuilder.AppendLine($"{depthSpacerText}public static {type} Build{genericParameters}(");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}this global::Macaron.InlineInterface.ImplementationOf<{type}> implementationOf,");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}global::Macaron.InlineInterface.Tag _ = default)");

        foreach (var constraint in genericParameterConstraints)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}{constraint}");
        }

        stringBuilder.AppendLine($"{depthSpacerText}{{");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new {globalTypeBuilder}(@base: implementationOf.Base).Build(_);");
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
