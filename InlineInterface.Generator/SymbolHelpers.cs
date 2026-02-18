using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.InlineInterface;

internal static class SymbolHelpers
{
    public sealed record MethodContext(
        string DelegateType,
        string Name,
        string UniqueName,
        string ParameterName,
        string FieldName,
        string Implementation
    );

    public static ImmutableArray<MethodContext> CreateMethodContexts(ImmutableArray<IMethodSymbol> methodSymbols)
    {
        var methodNameCounter = new Dictionary<string, int>();
        var builder = ImmutableArray.CreateBuilder<MethodContext>();

        foreach (var methodSymbol in methodSymbols)
        {
            var methodName = methodSymbol.Name;
            var uniqueName = methodNameCounter.TryGetValue(methodName, out var count)
                ? $"{methodName}_{count}"
                : $"{methodName}_0";
            var parameterName = $"{char.ToLowerInvariant(uniqueName[0])}{uniqueName[1..]}";
            var fieldName = $"_{parameterName}";

            methodNameCounter[methodName] = count + 1;

            var paramTypes = new List<string>();
            var parameters = new List<string>();
            var arguments = new List<string>();

            foreach (var paramSymbol in methodSymbol.Parameters)
            {
                var (type, name) = GetParameterString(paramSymbol);

                paramTypes.Add(type);
                parameters.Add($"{type} {name}");
                arguments.Add(name);
            };

            var paramTypeList = string.Join(", ", paramTypes);
            var paramList = string.Join(", ", parameters);
            var argList = string.Join(", ", arguments);

            string returnType;
            string delegateType;

            if (methodSymbol.ReturnsVoid)
            {
                returnType = "void";
                delegateType = paramTypeList.Length > 0
                    ? $"global::System.Action<{string.Join(", ", paramTypeList)}>"
                    : $"global::System.Action";
            }
            else
            {
                returnType = methodSymbol.ReturnType.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
                    IncludeNullableReferenceTypeModifier |
                    UseSpecialTypes
                ));
                delegateType = paramTypeList.Length > 0
                    ? $"global::System.Func<{paramTypeList}, {returnType}>"
                    : $"global::System.Func<{returnType}>";
            }

            builder.Add(new MethodContext(
                DelegateType: delegateType,
                Name: methodName,
                UniqueName: uniqueName,
                ParameterName: parameterName,
                FieldName: fieldName,
                Implementation: $"public {returnType} {methodName}({paramList}) => {fieldName}({argList});"
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
}
