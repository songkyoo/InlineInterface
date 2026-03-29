using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.InlineInterface;

internal static class SymbolHelpers
{
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

    public sealed record MethodContext(
        string DelegateType,
        string Name,
        string UniqueName,
        string ParameterName,
        string FieldName,
        string Implementation
    );

    public static ImmutableArray<PropertyContext> CreatePropertyContexts(
        string interfaceType,
        ImmutableArray<IPropertySymbol> propertySymbols,
        string indent,
        bool hasEventMembers
    )
    {
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

    public static ImmutableArray<MethodContext> CreateMethodContexts(
        string interfaceType,
        ImmutableArray<IMethodSymbol> methodSymbols,
        bool hasEventMembers
    )
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

            if (hasEventMembers)
            {
                paramTypes.Add("EventCollection");
                arguments.Add("_eventCollection");
            }

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
                Implementation: $"{returnType} {interfaceType}.{methodName}({paramList}) => ({fieldName} ?? throw new global::System.NotImplementedException())({argList});"
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
