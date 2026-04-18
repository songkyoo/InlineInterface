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
            genericParameterMap
        );
        var eventCodeGenerator = new EventCodeGenerator(
            eventContextProvider,
            interfaceTypeStringProvider,
            genericParameterMap,
            Indent
        );
        var hasEventMembers = eventContextProvider.Contexts.Any();

        var propertyContextProvider = new PropertyContextProvider(
            propertySymbols,
            genericParameterMap,
            globalTypeBuilder,
            hasEventMembers
        );
        var propertyCodeGenerator = new PropertyCodeGenerator(
            propertyContextProvider,
            interfaceTypeStringProvider,
            typeBuilder,
            Indent
        );

        var methodContextProvider = new MethodContextProvider(
            methodSymbols,
            genericParameterMap,
            globalTypeBuilder,
            hasEventMembers
        );
        var methodCodeGenerator = new MethodCodeGenerator(
            methodContextProvider,
            interfaceTypeStringProvider,
            typeBuilder,
            Indent
        );

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

            foreach (var line in eventCodeGenerator.GetEventCollectionFieldDeclarations())
            {
                stringBuilder.AppendLine($"{depthSpacerText}{line}");
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

            foreach (var lines in eventCodeGenerator.GetEventDispatcherImplementations())
            {
                stringBuilder.AppendLine();

                foreach (var line in lines)
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

        foreach (var line in methodCodeGenerator.GetImplFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        stringBuilder.AppendLine();

        // impl constructor parameters
        stringBuilder.Append($"{depthSpacerText}public Impl(");

        var implConstructorParams = new List<string>();

        implConstructorParams.AddRange(propertyCodeGenerator.GetImplConstructorParameterFragments());
        implConstructorParams.AddRange(methodCodeGenerator.GetImplConstructorParameterFragments());

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

        foreach (var line in methodCodeGenerator.GetImplConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        // end impl constructor body
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");

        // impl event implementations
        foreach (var eventSymbol in eventSymbols)
        {
            stringBuilder.AppendLine();

            foreach (var line in eventCodeGenerator.GetInterfaceImplementation(eventSymbol))
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
        foreach (var methodSymbol in methodSymbols)
        {
            var line = methodCodeGenerator.GetInterfaceImplementation(methodSymbol);

            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
        }

        // end impl type
        depthSpacerText = depthSpacerText[..^Indent.Length];

        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();

        // builder field members
        stringBuilder.AppendLine($"{depthSpacerText}private const string InterfaceDisplayName = {ToStringLiteral(type)};");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}private static global::System.InvalidOperationException CreateMissingBuildDelegateException(string memberDescription)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new global::System.InvalidOperationException($\"Cannot build inline implementation for '{{InterfaceDisplayName}}' because no delegate was provided for {{memberDescription}}. Pass a delegate or set allowMissingImplementation: true.\");");
        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}private static global::System.NotImplementedException CreateMissingInvocationDelegateException(string memberDescription)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");
        stringBuilder.AppendLine($"{depthSpacerText}{Indent}return new global::System.NotImplementedException($\"No delegate was configured for {{memberDescription}} on '{{InterfaceDisplayName}}'. This can happen when Build was called with allowMissingImplementation: true.\");");
        stringBuilder.AppendLine($"{depthSpacerText}}}");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"{depthSpacerText}private readonly bool _allowMissingImplementation;");
        stringBuilder.AppendLine();

        foreach (var line in propertyCodeGenerator.GetBuilderFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        foreach (var line in methodCodeGenerator.GetBuilderFieldDeclarations())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        // builder constructor parameters
        stringBuilder.Append($"{depthSpacerText}public {mergedTypePrefix}Builder(");
        stringBuilder.AppendLine();
        stringBuilder.Append($"{depthSpacerText}{Indent}bool allowMissingImplementation");

        var builderConstructorParams = new List<string>();

        builderConstructorParams.AddRange(propertyCodeGenerator.GetBuilderConstructorParameterFragments());
        builderConstructorParams.AddRange(methodCodeGenerator.GetBuilderConstructorParameterFragments());

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

        foreach (var line in methodCodeGenerator.GetBuilderConstructorAssignments())
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
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

        foreach (var line in methodCodeGenerator.GetBuilderMethodImplementation(typeBuilder))
        {
            stringBuilder.AppendLine($"{depthSpacerText}{line}");
            stringBuilder.AppendLine();
        }

        // begin build method
        stringBuilder.AppendLine($"{depthSpacerText}public {type} Build(global::Macaron.InlineInterface.Tag _ = default)");
        stringBuilder.AppendLine($"{depthSpacerText}{{");

        depthSpacerText += Indent;

        stringBuilder.Append($"{depthSpacerText}return new Impl(");

        var implConstructorArgs = new List<string>();

        implConstructorArgs.AddRange(propertyCodeGenerator.GetBuildArgumentFragments());
        implConstructorArgs.AddRange(methodCodeGenerator.GetBuildArgumentFragments());

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

        foreach (var lines in methodCodeGenerator.GetExtensionMethodImplementation(
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
        var assemblyName = typeSymbol.ContainingAssembly != null ? $"{typeSymbol.ContainingAssembly}" : "";
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

    private static string ToStringLiteral(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
