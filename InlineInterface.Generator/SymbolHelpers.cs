using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.InlineInterface;

public sealed record PropertyContext(
    string? GetterDelegateType,
    string? SetterDelegateType,
    string Name,
    string? GetterName,
    string? SetterName,
    string? GetterParameterName,
    string? SetterParameterName,
    string? GetterFieldName,
    string? SetterFieldName,
    ImmutableArray<string> Implementation
);

public sealed record EventContext(
    ImmutableArray<string> Implementation
);

internal static class SymbolHelpers
{
    public static ImmutableArray<EventContext> CreateEventContexts(
        INamedTypeSymbol interfaceSymbol,
        ImmutableArray<IEventSymbol> eventSymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string indent
    )
    {
        var interfaceType = GetTypeStrings(interfaceSymbol, genericParameterMap);
        var builder = ImmutableArray.CreateBuilder<EventContext>();

        foreach (var eventSymbol in eventSymbols)
        {
            if (eventSymbol.Type is not INamedTypeSymbol namedTypeSymbol)
            {
                continue;
            }

            var eventType = GetTypeStrings(namedTypeSymbol, genericParameterMap);

            if (eventSymbol.NullableAnnotation != NullableAnnotation.Annotated)
            {
                eventType += "?";
            }

            var implementationBuilder = ImmutableArray.CreateBuilder<string>();

            implementationBuilder.Add($"event {eventType} {interfaceType}.{eventSymbol.Name}");
            implementationBuilder.Add($"{{");
            implementationBuilder.Add($"{indent}add => _eventCollection.{eventSymbol.Name} += value;");
            implementationBuilder.Add($"{indent}remove => _eventCollection.{eventSymbol.Name} -= value;");
            implementationBuilder.Add($"}}");

            builder.Add(new EventContext(
                Implementation: implementationBuilder.ToImmutable()
            ));
        }

        return builder.ToImmutable();
    }

    public static ImmutableArray<PropertyContext> CreatePropertyContexts(
        INamedTypeSymbol interfaceSymbol,
        ImmutableArray<IPropertySymbol> propertySymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string indent,
        bool hasEventMembers
    )
    {
        var interfaceType = GetTypeStrings(interfaceSymbol, genericParameterMap);
        var builder = ImmutableArray.CreateBuilder<PropertyContext>();

        foreach (var propertySymbol in propertySymbols)
        {
            var propertyName = propertySymbol.Name;
            var propertyType = propertySymbol.Type.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
                IncludeNullableReferenceTypeModifier |
                UseSpecialTypes
            ));

            string? getterDelegateType;
            string? setterDelegateType;
            string? getterName;
            string? setterName;
            string? getterParameterName;
            string? setterParameterName;
            string? getterFieldName;
            string? setterFieldName;
            var implementationLines = ImmutableArray.CreateBuilder<string>();

            implementationLines.Add($"{propertyType} {interfaceType}.{propertyName}");
            implementationLines.Add($"{{");

            var eventCollectionTypeParam = hasEventMembers ? "EventCollection, " : "";

            if (propertySymbol.GetMethod != null)
            {
                getterDelegateType = $"global::System.Func<{eventCollectionTypeParam}{propertyType}>";
                getterName = $"Get{propertyName}";
                getterParameterName = $"get{propertyName}";
                getterFieldName = $"_{getterParameterName}";

                implementationLines.Add($"{indent}get => ({getterFieldName} ?? throw new global::System.NotImplementedException())({(hasEventMembers ? "_eventCollection" : "")});");
            }
            else
            {
                getterDelegateType = null;
                getterName = null;
                getterParameterName = null;
                getterFieldName = null;
            }

            if (propertySymbol.SetMethod != null)
            {
                setterDelegateType = $"global::System.Action<{eventCollectionTypeParam}{propertyType}>";
                setterName = $"Set{propertyName}";
                setterParameterName = $"set{propertyName}";
                setterFieldName = $"_{setterParameterName}";

                implementationLines.Add($"{indent}set => ({setterFieldName} ?? throw new global::System.NotImplementedException())({(hasEventMembers ? "_eventCollection, " : "")}value);");
            }
            else
            {
                setterDelegateType = null;
                setterName = null;
                setterParameterName = null;
                setterFieldName = null;
            }

            implementationLines.Add($"}}");

            builder.Add(new PropertyContext(
                GetterDelegateType: getterDelegateType,
                SetterDelegateType: setterDelegateType,
                Name: propertyName,
                GetterName: getterName,
                SetterName: setterName,
                GetterParameterName: getterParameterName,
                SetterParameterName: setterParameterName,
                GetterFieldName: getterFieldName,
                SetterFieldName: setterFieldName,
                Implementation: implementationLines.ToImmutable()
            ));
        }

        return builder.ToImmutable();
    }

    public static bool HasDuplicatedTypeParameterName(ImmutableArray<INamedTypeSymbol> typeSymbols)
    {
        var seen = new HashSet<string>();

        return typeSymbols.SelectMany(symbol => symbol.TypeParameters).Any(typeParam => !seen.Add(typeParam.Name));
    }

    public static ImmutableArray<INamedTypeSymbol> GetNestedTypeSymbols(INamedTypeSymbol typeSymbol)
    {
        var typeSymbols = new List<INamedTypeSymbol>();

        var parentTypeSymbol = typeSymbol;
        while (parentTypeSymbol != null)
        {
            typeSymbols.Add(parentTypeSymbol);
            parentTypeSymbol = parentTypeSymbol.ContainingType;
        }

        typeSymbols.Reverse();

        return typeSymbols.ToImmutableArray();
    }

    public static string GetTypeParameterConstraintClause(
        ITypeParameterSymbol typeParameterSymbol,
        Func<string, string> nameSelector
    )
    {
        var constraints = new List<string>();

        if (typeParameterSymbol.HasReferenceTypeConstraint)
        {
            constraints.Add("class");
        }

        if (typeParameterSymbol.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }

        if (typeParameterSymbol.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }

        foreach (var constraintType in typeParameterSymbol.ConstraintTypes)
        {
            constraints.Add(constraintType.ToDisplayString(FullyQualifiedFormat));
        }

        if (typeParameterSymbol.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        if (typeParameterSymbol.HasNotNullConstraint)
        {
            constraints.Add("not null");
        }

        return constraints.Count > 0
            ? $"where {nameSelector(typeParameterSymbol.Name)} : {string.Join(", ", constraints)}"
            : "";
    }

    public static string GetTypeStrings(
        INamedTypeSymbol typeSymbol,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var typeSymbols = GetNestedTypeSymbols(typeSymbol);
        var @namespace = typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString()
            : "";
        var types = new List<string>();

        foreach (var symbol in typeSymbols)
        {
            var builder = new StringBuilder(symbol.Name);

            if (symbol.Arity > 0)
            {
                builder.Append("<");

                for (int i = 0; i < symbol.TypeArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    var typeArgumentSymbol = symbol.TypeArguments[i];
                    if (typeArgumentSymbol is ITypeParameterSymbol typeParameterSymbol)
                    {
                        builder.Append(genericParameterMap[typeParameterSymbol]);
                    }
                    else
                    {
                        builder.Append(typeArgumentSymbol.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
                            IncludeNullableReferenceTypeModifier |
                            UseSpecialTypes
                        )));
                    }
                }

                builder.Append(">");
            }

            types.Add(builder.ToString());
        }

        return $"global::{(@namespace.Length > 0 ? $"{@namespace}." : "")}{string.Join(".", types)}";
    }
}
