using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using static Macaron.InlineInterface.SymbolHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

internal static class SourceGenerationHelpers
{
    const string Indent = "    ";

    public static void AddSource(
        SourceProductionContext context,
        INamedTypeSymbol typeSymbol,
        ImmutableArray<InterfaceContext> interfaceContexts
    )
    {
        var (
            type,
            genericParameters,
            genericParameterConstraints,
            genericParameterMap
        ) = GetTypeStrings(typeSymbol);


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
        var typeBuilderNamespace = $"Macaron.InlineInterface.Generated{GetNamespaceString(typeSymbol)}";
        var typeBuilder = $"{mergedTypePrefix}Builder{genericParameters}";
        var globalTypeBuilder = $"global::{typeBuilderNamespace}.{typeBuilder}";

        var eventSymbols = interfaceContexts.SelectMany(ctx => ctx.EventSymbols).ToImmutableArray();
        var propertySymbols = interfaceContexts.SelectMany(ctx => ctx.PropertySymbols).ToImmutableArray();
        var methodSymbols = interfaceContexts.SelectMany(ctx => ctx.MethodSymbols).ToImmutableArray();

        var interfaceTypeStringProvider = new InterfaceTypeStringProvider(genericParameterMap);

        var eventContextProvider = new EventContextProvider(
            eventSymbols,
            genericParameterMap,
            interfaceTypeStringProvider,
            Indent
        );
        var eventContexts = eventContextProvider.Contexts.ToImmutableArray();
        var hasEventMembers = eventContexts.Any();

        var propertyContextProvider = new PropertyContextProvider(
            propertySymbols,
            genericParameterMap,
            globalTypeBuilder,
            hasEventMembers
        );
        var propertyCodeGenerator = new PropertyCodeGenerator(
            propertyContextProvider,
            interfaceTypeStringProvider,
            Indent
        );

        var methodContextProvider = new MethodContextProvider(
            methodSymbols,
            genericParameterMap,
            interfaceTypeStringProvider,
            globalTypeBuilder,
            hasEventMembers
        );
        var methodContexts = methodContextProvider.Contexts.ToImmutableArray();

        var stringBuilder = CreateStringBuilderWithFileHeader();
        var depthSpacerText = "";

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

        if (hasEventMembers)
        {
            // EventCollection
            stringBuilder.AppendLine($"{depthSpacerText}public sealed class EventCollection");
            stringBuilder.AppendLine($"{depthSpacerText}{{");

            depthSpacerText += Indent;

            foreach (var eventContext in eventContexts)
            {
                stringBuilder.AppendLine($"{depthSpacerText}public {eventContext.Type} {eventContext.UniqueName};");
            }

            depthSpacerText = depthSpacerText[..^Indent.Length];

            stringBuilder.AppendLine($"{depthSpacerText}}}");
            stringBuilder.AppendLine();

            // EventDispatcher
            stringBuilder.AppendLine($"{depthSpacerText}public sealed class EventDispatcher");
            stringBuilder.AppendLine($"{depthSpacerText}{{");

            depthSpacerText += Indent;

            stringBuilder.AppendLine($"{depthSpacerText}private readonly EventCollection _eventCollection;");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"{depthSpacerText}public EventDispatcher(EventCollection eventCollection)");
            stringBuilder.AppendLine($"{depthSpacerText}{{");
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}_eventCollection = eventCollection;");
            stringBuilder.AppendLine($"{depthSpacerText}}}");

            foreach (var eventSymbol in eventSymbols)
            {
                stringBuilder.AppendLine();

                foreach (var line in eventContextProvider.GetEventDispatcherImplementation(eventSymbol))
                {
                    stringBuilder.AppendLine($"{depthSpacerText}{line}");
                }
            }

            depthSpacerText = depthSpacerText[..^Indent.Length];

            stringBuilder.AppendLine($"{depthSpacerText}}}");
            stringBuilder.AppendLine();
        }

        // begin impl type
        stringBuilder.AppendLine($"{depthSpacerText}private sealed class Impl : {type}");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        if (hasEventMembers)
        {
            stringBuilder.AppendLine($"{depthSpacerText}private readonly EventCollection _eventCollection = new();");
            stringBuilder.AppendLine($"{depthSpacerText}private readonly EventDispatcher _eventDispatcher;");
        }

        foreach (var line in propertyCodeGenerator.GetImplFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}private readonly {methodContext.DelegateType}? {methodContext.FieldName};");
        }

        stringBuilder.AppendLine();

        // impl constructor parameters
        stringBuilder.Append($"{depthSpacerText}public Impl(");

        var implConstructorParams = new List<string>();

        implConstructorParams.AddRange(propertyCodeGenerator.GetImplConstructorParameterFragments());

        foreach (var methodContext in methodContexts)
        {
            implConstructorParams.Add($"{methodContext.DelegateType}? {methodContext.ParameterName}");
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

        if (hasEventMembers)
        {
            stringBuilder.AppendLine($"{depthSpacerText}_eventDispatcher = new EventDispatcher(_eventCollection);");
        }

        foreach (var line in propertyCodeGenerator.GetImplConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}{methodContext.FieldName} = {methodContext.ParameterName};");
        }

        // end impl constructor body
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // impl event implementations
        foreach (var eventSymbol in eventSymbols)
        {
            stringBuilder.AppendLine();

            foreach (var line in eventContextProvider.GetInterfaceImplementation(eventSymbol))
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }
        }

        // impl property implementations
        foreach (var propertySymbol in propertySymbols)
        {
            stringBuilder.AppendLine();

            foreach (var line in propertyCodeGenerator.GetInterfaceImplementation(propertySymbol))
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }
        }

        // impl method implementations
        foreach (var methodImplementation in methodSymbols.Select(methodContextProvider.GetInterfaceImplementation))
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"{depthSpacerText}{methodImplementation}");
        }

        // end impl type
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // builder field members
        stringBuilder.AppendLine($"{depthSpacerText}private readonly bool _allowMissingImplementation;");
        stringBuilder.AppendLine();

        foreach (var line in propertyCodeGenerator.GetBuilderFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        foreach (var methodContext in methodContexts)
        {
            stringBuilder.AppendLine($"{depthSpacerText}private readonly {methodContext.DelegateType}? {methodContext.UniqueName} {{ get; init; }} = null;");
            stringBuilder.AppendLine();
        }

        // builder constructor parameters
        stringBuilder.Append($"{depthSpacerText}public {mergedTypePrefix}Builder(");
        stringBuilder.AppendLine();
        stringBuilder.Append($"{depthSpacerText}{Indent}bool allowMissingImplementation");

        var builderConstructorParams = new List<string>();

        builderConstructorParams.AddRange(propertyCodeGenerator.GetBuilderConstructorParameterFragments());

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

        stringBuilder.AppendLine($"{depthSpacerText}_allowMissingImplementation = allowMissingImplementation;");
        stringBuilder.AppendLine();

        foreach (var line in propertyCodeGenerator.GetBuilderConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
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
        foreach (var line in propertyCodeGenerator.GetBuilderMethodImplementation(typeBuilder))
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
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

        stringBuilder.Append($"{depthSpacerText}return new Impl(");

        var implConstructorArgs = new List<string>();

        implConstructorArgs.AddRange(propertyCodeGenerator.GetBuildArgumentFragments());

        foreach (var methodContext in methodContexts)
        {
            implConstructorArgs.Add($"{methodContext.ParameterName}: {methodContext.UniqueName} ?? (_allowMissingImplementation ? null : throw new global::System.InvalidOperationException())");
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
        foreach (var lines in propertyCodeGenerator.GetExtensionMethodImplementation(
            type,
            globalTypeBuilder,
            genericParameters,
            genericParameterConstraints
        ))
        {
            foreach (var line in lines)
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
            }

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
            stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new {globalTypeBuilder}(allowMissingImplementation: implementationOf.AllowMissingImplementation, {methodContext.ParameterName}: impl);");
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
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new {globalTypeBuilder}(allowMissingImplementation: implementationOf.AllowMissingImplementation).Build(_);");
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
        ImmutableArray<string> GenericParameterConstraints,
        ImmutableDictionary<ITypeParameterSymbol, string> GenericParameterMap
    ) GetTypeStrings(INamedTypeSymbol typeSymbol)
    {
        var typeSymbols = GetNestedTypeSymbols(typeSymbol);
        var typeParameters = typeSymbols
            .SelectMany(static symbol => symbol.TypeParameters)
            .ToArray();

        var genericParameterMap = CreateGenericParameterMap(typeSymbols);

        var genericParameterConstraints = typeParameters
            .Select(symbol => GetTypeParameterConstraintClause(
                typeParameterSymbol: symbol,
                typeParameterNameSelector: symbol2 => genericParameterMap[symbol2],
                typeStringSelector: type => GetTypeString(type, genericParameterMap)
            ))
            .Where(static clause => clause.Length > 0)
            .ToImmutableArray();

        var type = GetTypeString(typeSymbol, genericParameterMap);

        var genericParameters = typeParameters.Length > 0
            ? $"<{string.Join(", ", typeParameters.Select(symbol => genericParameterMap[symbol]))}>"
            : "";

        return (
            Type: type,
            GenericParameters: genericParameters,
            GenericParameterConstraints: genericParameterConstraints,
            GenericParameterMap: genericParameterMap
        );

        #region Local Functions
        static ImmutableDictionary<ITypeParameterSymbol, string> CreateGenericParameterMap(
            ImmutableArray<INamedTypeSymbol> typeSymbols
        )
        {
            var builder = ImmutableDictionary.CreateBuilder<ITypeParameterSymbol, string>(
                SymbolEqualityComparer.Default
            );

            if (!HasDuplicatedTypeParameterName(typeSymbols))
            {
                foreach (var typeParameter in typeSymbols.SelectMany(static symbol => symbol.TypeParameters))
                {
                    builder.Add(typeParameter, typeParameter.Name);
                }
            }
            else
            {
                var index = 0;

                foreach (var typeParameter in typeSymbols.SelectMany(static symbol => symbol.TypeParameters))
                {
                    builder.Add(typeParameter, $"T{index}");
                    index += 1;
                }
            }

            return builder.ToImmutable();
        }
        #endregion
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
