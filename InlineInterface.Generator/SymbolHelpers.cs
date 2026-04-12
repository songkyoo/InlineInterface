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

internal static class SymbolHelpers
{
    public static ImmutableArray<PropertyContext> CreatePropertyContexts(
        INamedTypeSymbol interfaceSymbol,
        ImmutableArray<IPropertySymbol> propertySymbols,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap,
        string indent,
        bool hasEventMembers
    )
    {
        var interfaceType = GetTypeString(interfaceSymbol, genericParameterMap);
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
        Func<ITypeParameterSymbol, string> typeParameterNameSelector,
        Func<ITypeSymbol, string> typeStringSelector
    )
    {
        var constraints = new List<string>();

        if (typeParameterSymbol.HasUnmanagedTypeConstraint)
        {
            constraints.Add("unmanaged");
        }
        else if (typeParameterSymbol.HasValueTypeConstraint)
        {
            constraints.Add("struct");
        }
        else if (typeParameterSymbol.HasReferenceTypeConstraint)
        {
            constraints.Add(
                typeParameterSymbol.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class"
            );
        }
        else if (typeParameterSymbol.HasNotNullConstraint)
        {
            constraints.Add("notnull");
        }

        ITypeSymbol? baseTypeConstraint = null;
        var interfaceConstraints = new List<ITypeSymbol>();

        foreach (var constraintType in typeParameterSymbol.ConstraintTypes)
        {
            if (constraintType.TypeKind == TypeKind.Class)
            {
                baseTypeConstraint ??= constraintType;
            }
            else
            {
                interfaceConstraints.Add(constraintType);
            }
        }

        if (baseTypeConstraint != null)
        {
            constraints.Add(typeStringSelector(baseTypeConstraint));
        }

        foreach (var interfaceConstraint in interfaceConstraints)
        {
            constraints.Add(typeStringSelector(interfaceConstraint));
        }

        if (typeParameterSymbol.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count > 0
            ? $"where {typeParameterNameSelector(typeParameterSymbol)} : {string.Join(", ", constraints)}"
            : "";
    }

    public static string GetTypeString(
        ITypeSymbol typeSymbol,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        string typeString;

        switch (typeSymbol)
        {
            case INamedTypeSymbol namedTypeSymbol:
            {
                var specialTypeKeyword = GetSpecialTypeKeyword(namedTypeSymbol);

                typeString = specialTypeKeyword ?? GetNamedTypeString(namedTypeSymbol, genericParameterMap);

                break;
            }
            case ITypeParameterSymbol typeParameterSymbol:
            {
                typeString =  genericParameterMap.TryGetValue(typeParameterSymbol, out var mapped)
                    ? mapped
                    : typeParameterSymbol.Name;

                break;
            }
            case IArrayTypeSymbol arrayTypeSymbol:
            {
                var brackets = $"[{new string(',', arrayTypeSymbol.Rank - 1)}]";

                typeString = $"{GetTypeString(arrayTypeSymbol.ElementType, genericParameterMap)}{brackets}";

                break;
            }
            default:
            {
                typeString = typeSymbol.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
                    IncludeNullableReferenceTypeModifier |
                    UseSpecialTypes
                ));

                break;
            }
        }

        if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated && !typeString.EndsWith("?"))
        {
            typeString += "?";
        }

        return typeString;
    }

    private static string GetNamedTypeString(
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

                for (var i = 0; i < symbol.TypeArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(GetTypeString(symbol.TypeArguments[i], genericParameterMap));
                }

                builder.Append(">");
            }

            types.Add(builder.ToString());
        }

        return $"global::{(@namespace.Length > 0 ? $"{@namespace}." : "")}{string.Join(".", types)}";
    }

    private static string? GetSpecialTypeKeyword(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.SpecialType switch
        {
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Byte => "byte",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Char => "char",
            SpecialType.System_String => "string",
            SpecialType.System_Object => "object",
            SpecialType.System_Void => "void",
            _ => null,
        };
    }
}
