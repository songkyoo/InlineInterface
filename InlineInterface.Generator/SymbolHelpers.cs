using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.InlineInterface;

public static class SymbolHelpers
{
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
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
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
            constraints.Add(GetTypeString(baseTypeConstraint, genericParameterMap));
        }

        foreach (var interfaceConstraint in interfaceConstraints)
        {
            constraints.Add(GetTypeString(interfaceConstraint, genericParameterMap));
        }

        if (typeParameterSymbol.HasConstructorConstraint)
        {
            constraints.Add("new()");
        }

        return constraints.Count > 0
            ? $"where {genericParameterMap[typeParameterSymbol]} : {string.Join(", ", constraints)}"
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
                typeString = GetNamedTypeString(namedTypeSymbol, genericParameterMap);

                break;
            }
            case ITypeParameterSymbol typeParameterSymbol:
            {
                typeString = genericParameterMap.TryGetValue(typeParameterSymbol, out var mapped)
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
        if (GetSpecialTypeKeyword(typeSymbol) is { } keyword)
        {
            return keyword;
        }

        if (typeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return GetTypeString(typeSymbol.TypeArguments[0], genericParameterMap) + "?";
        }

        return GetFullyQualifiedNamedTypeString(typeSymbol, genericParameterMap);
    }

    private static string GetFullyQualifiedNamedTypeString(
        INamedTypeSymbol typeSymbol,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var typeSymbols = GetNestedTypeSymbols(typeSymbol);
        var @namespace = typeSymbol.ContainingNamespace is
            {
                IsGlobalNamespace: false
            } containingNamespace
            ? containingNamespace.ToDisplayString()
            : "";
        var builder = new StringBuilder("global::");

        if (@namespace.Length > 0)
        {
            builder.Append(@namespace);
            builder.Append(".");
        }

        for (var typeIndex = 0; typeIndex < typeSymbols.Length; typeIndex++)
        {
            if (typeIndex > 0)
            {
                builder.Append(".");
            }

            var symbol = typeSymbols[typeIndex];

            builder.Append(symbol.Name);

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
        }

        return builder.ToString();
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
