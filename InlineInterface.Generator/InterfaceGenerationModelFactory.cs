using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.SymbolHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

public static class InterfaceGenerationModelFactory
{
    #region Static Methods
    public static InterfaceGenerationModel Create(
        INamedTypeSymbol typeSymbol,
        ImmutableArray<InterfaceContext> interfaceContexts
    )
    {
        var (
            type,
            genericParameters,
            genericParameterConstraints,
            genericParameterMap,
            mergedTypePrefix
        ) = GetTypeStrings(typeSymbol);

        var typeBuilderNamespace = $"Macaron.InlineInterface.Generated{GetNamespaceString(typeSymbol)}";
        var typeBuilder = $"{mergedTypePrefix}Builder{genericParameters}";
        var globalTypeBuilder = $"global::{typeBuilderNamespace}.{typeBuilder}";

        var interfaceTypeProvider = new InterfaceTypeProvider(
            genericParameterMap,
            interfaceContexts.Length
        );
        var (
            eventSymbols,
            propertySymbols,
            methodSymbols
        ) = CollectMemberSymbols(interfaceContexts);

        var eventContextProvider = new EventContextProvider(
            eventSymbols,
            genericParameterMap
        );
        var eventModels = eventContextProvider.Models;
        var eventGenerationModelIndicesByImplementation =
            eventContextProvider.GenerationModelIndicesByImplementation;
        var eventImplementationBuilder = ImmutableArray.CreateBuilder<EventImplementationModel>();

        for (var i = 0; i < eventSymbols.Length; i++)
        {
            var eventModelIndex = eventGenerationModelIndicesByImplementation[i];

            if (eventModelIndex >= 0)
            {
                var eventSymbol = eventSymbols[i];

                eventImplementationBuilder.Add(new EventImplementationModel(
                    eventModelIndex,
                    interfaceTypeProvider.GetIndex(eventSymbol.ContainingType)
                ));
            }
        }

        var eventImplementations = eventImplementationBuilder.ToImmutable();
        var hasEventMembers = eventModels.Length > 0;

        var propertyContextProvider = new PropertyContextProvider(
            propertySymbols,
            genericParameterMap,
            globalTypeBuilder,
            hasEventMembers
        );
        var propertyModels = propertyContextProvider.Models;
        var propertyGenerationModelIndicesByImplementation =
            propertyContextProvider.GenerationModelIndicesByImplementation;
        var propertyImplementationBuilder = ImmutableArray.CreateBuilder<PropertyImplementationModel>();

        for (var i = 0; i < propertySymbols.Length; i++)
        {
            var propertySymbol = propertySymbols[i];

            propertyImplementationBuilder.Add(new PropertyImplementationModel(
                propertyGenerationModelIndicesByImplementation[i],
                interfaceTypeProvider.GetIndex(propertySymbol.ContainingType),
                HasGetter: propertySymbol.GetMethod != null,
                HasSetter: propertySymbol.SetMethod != null
            ));
        }

        var propertyImplementations = propertyImplementationBuilder.ToImmutable();

        var methodContextProvider = new MethodContextProvider(
            methodSymbols,
            genericParameterMap,
            globalTypeBuilder,
            hasEventMembers
        );
        var methodModels = methodContextProvider.Models;
        var methodGenerationModelIndicesByImplementation =
            methodContextProvider.GenerationModelIndicesByImplementation;
        var methodImplementationBuilder = ImmutableArray.CreateBuilder<MethodImplementationModel>();

        for (var i = 0; i < methodSymbols.Length; i++)
        {
            var methodSymbol = methodSymbols[i];

            methodImplementationBuilder.Add(new MethodImplementationModel(
                methodGenerationModelIndicesByImplementation[i],
                interfaceTypeProvider.GetIndex(methodSymbol.ContainingType)
            ));
        }

        var methodImplementations = methodImplementationBuilder.ToImmutable();

        return new InterfaceGenerationModel(
            Type: type,
            GenericParameters: genericParameters,
            GenericParameterConstraints: genericParameterConstraints,
            MergedTypePrefix: mergedTypePrefix,
            TypeBuilderNamespace: typeBuilderNamespace,
            TypeBuilder: typeBuilder,
            GlobalTypeBuilder: globalTypeBuilder,
            InterfaceTypes: interfaceTypeProvider.ToImmutableArray(),
            Events: eventModels,
            EventImplementations: eventImplementations,
            Properties: propertyModels,
            PropertyImplementations: propertyImplementations,
            Methods: methodModels,
            MethodImplementations: methodImplementations,
            HintName: GetHintName(typeSymbol)
        );
    }

    private static (
        ImmutableArray<IEventSymbol> EventSymbols,
        ImmutableArray<IPropertySymbol> PropertySymbols,
        ImmutableArray<IMethodSymbol> MethodSymbols
    ) CollectMemberSymbols(ImmutableArray<InterfaceContext> interfaceContexts)
    {
        var eventBuilder = ImmutableArray.CreateBuilder<IEventSymbol>();
        var propertyBuilder = ImmutableArray.CreateBuilder<IPropertySymbol>();
        var methodBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var interfaceContext in interfaceContexts)
        {
            eventBuilder.AddRange(interfaceContext.EventSymbols);
            propertyBuilder.AddRange(interfaceContext.PropertySymbols);
            methodBuilder.AddRange(interfaceContext.MethodSymbols);
        }

        return (
            EventSymbols: eventBuilder.ToImmutable(),
            PropertySymbols: propertyBuilder.ToImmutable(),
            MethodSymbols: methodBuilder.ToImmutable()
        );
    }

    private static string GetNamespaceString(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? $".{ns.ToDisplayString()}" : "";
    }

    private static string GetTypeName(INamedTypeSymbol typeSymbol)
    {
        return $"{typeSymbol.Name}{(typeSymbol.Arity > 0 ? $"_{typeSymbol.Arity}" : "")}";
    }

    private static (
        string Type,
        string GenericParameters,
        ImmutableArray<string> GenericParameterConstraints,
        ImmutableDictionary<ITypeParameterSymbol, string> GenericParameterMap,
        string MergedTypePrefix
    ) GetTypeStrings(INamedTypeSymbol typeSymbol)
    {
        List<ITypeParameterSymbol>? typeParameters = null;
        HashSet<string>? typeParameterNames = null;
        var hasDuplicatedTypeParameterName = false;
        var mergedTypePrefixBuilder = new StringBuilder();

        var typeSymbols = GetNestedTypeSymbols(typeSymbol);

        foreach (var symbol in typeSymbols)
        {
            if (mergedTypePrefixBuilder.Length > 0)
            {
                mergedTypePrefixBuilder.Append("_");
            }

            mergedTypePrefixBuilder.Append(GetTypeName(symbol));

            foreach (var typeParameter in symbol.TypeParameters)
            {
                typeParameters ??= [];
                typeParameterNames ??= new HashSet<string>(StringComparer.Ordinal);

                typeParameters.Add(typeParameter);
                hasDuplicatedTypeParameterName |= !typeParameterNames.Add(typeParameter.Name);
            }
        }

        var genericParameterMapBuilder = ImmutableDictionary.CreateBuilder<ITypeParameterSymbol, string>(
            SymbolEqualityComparer.Default
        );

        if (typeParameters is not null)
        {
            for (var i = 0; i < typeParameters.Count; i++)
            {
                var typeParameter = typeParameters[i];

                genericParameterMapBuilder.Add(
                    typeParameter,
                    hasDuplicatedTypeParameterName ? $"T{i}" : typeParameter.Name
                );
            }
        }

        var genericParameterMap = genericParameterMapBuilder.ToImmutable();
        var genericParameters = "";
        var genericParameterConstraints = ImmutableArray<string>.Empty;

        if (typeParameters is not null)
        {
            var genericParameterConstraintsBuilder = ImmutableArray.CreateBuilder<string>(typeParameters.Count);
            var genericParametersBuilder = new StringBuilder("<");

            for (var i = 0; i < typeParameters.Count; i++)
            {
                var typeParameter = typeParameters[i];

                if (i > 0)
                {
                    genericParametersBuilder.Append(", ");
                }

                genericParametersBuilder.Append(genericParameterMap[typeParameter]);

                var constraint = GetTypeParameterConstraintClause(
                    typeParameter,
                    genericParameterMap
                );

                if (constraint.Length > 0)
                {
                    genericParameterConstraintsBuilder.Add(constraint);
                }
            }

            genericParametersBuilder.Append(">");
            genericParameters = genericParametersBuilder.ToString();
            genericParameterConstraints = genericParameterConstraintsBuilder.ToImmutable();
        }

        var type = GetTypeString(typeSymbol, genericParameterMap);

        return (
            Type: type,
            GenericParameters: genericParameters,
            GenericParameterConstraints: genericParameterConstraints,
            GenericParameterMap: genericParameterMap,
            MergedTypePrefix: mergedTypePrefixBuilder.ToString()
        );
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
    #endregion
}
