using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

using static Macaron.InlineInterface.ParameterStringHelpers;
using static Macaron.InlineInterface.SymbolHelpers;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

internal static class InterfaceGenerationModelFactory
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
        var eventContexts = eventContextProvider.Contexts.ToImmutableArray();
        var eventModels = eventContexts
            .Select(context => CreateEventGenerationModel(context, genericParameterMap))
            .ToImmutableArray();
        var eventModelMap = eventContexts
            .Zip(eventModels, static (context, model) => (context, model))
            .ToDictionary(static pair => pair.context, static pair => pair.model);
        var eventImplementationBuilder = ImmutableArray.CreateBuilder<EventImplementationModel>();

        foreach (var eventSymbol in eventSymbols)
        {
            if (eventContextProvider.TryGetEventContext(eventSymbol, out var eventContext))
            {
                eventImplementationBuilder.Add(new EventImplementationModel(
                    eventModelMap[eventContext],
                    interfaceTypeStringProvider.GetInterfaceTypeName(eventSymbol.ContainingType)
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
        var propertyContexts = propertyContextProvider.Contexts.ToImmutableArray();
        var propertyModels = propertyContexts
            .Select(CreatePropertyGenerationModel)
            .ToImmutableArray();
        var propertyModelMap = propertyContexts
            .Zip(propertyModels, static (context, model) => (context, model))
            .ToDictionary(static pair => pair.context, static pair => pair.model);
        var propertyImplementationBuilder = ImmutableArray.CreateBuilder<PropertyImplementationModel>();

        foreach (var propertySymbol in propertySymbols)
        {
            if (propertyContextProvider.TryGetPropertyContext(propertySymbol, out var propertyContext))
            {
                propertyImplementationBuilder.Add(new PropertyImplementationModel(
                    propertyModelMap[propertyContext],
                    interfaceTypeStringProvider.GetInterfaceTypeName(propertySymbol.ContainingType),
                    HasGetter: propertySymbol.GetMethod != null,
                    HasSetter: propertySymbol.SetMethod != null
                ));
            }
        }

        var propertyImplementations = propertyImplementationBuilder.ToImmutable();

        var methodContextProvider = new MethodContextProvider(
            methodSymbols,
            genericParameterMap,
            globalTypeBuilder,
            hasEventMembers
        );
        var methodContexts = methodContextProvider.Contexts.ToImmutableArray();
        var methodModels = methodContexts
            .Select(CreateMethodGenerationModel)
            .ToImmutableArray();
        var methodModelMap = methodContexts
            .Zip(methodModels, static (context, model) => (context, model))
            .ToDictionary(static pair => pair.context, static pair => pair.model);
        var methodImplementationBuilder = ImmutableArray.CreateBuilder<MethodImplementationModel>();

        foreach (var methodSymbol in methodSymbols)
        {
            if (methodContextProvider.TryGetMethodContext(methodSymbol, out var methodContext))
            {
                methodImplementationBuilder.Add(new MethodImplementationModel(
                    methodModelMap[methodContext],
                    interfaceTypeStringProvider.GetInterfaceTypeName(methodSymbol.ContainingType)
                ));
            }
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
            Events: eventModels,
            EventImplementations: eventImplementations,
            Properties: propertyModels,
            PropertyImplementations: propertyImplementations,
            Methods: methodModels,
            MethodImplementations: methodImplementations,
            HintName: GetHintName(typeSymbol)
        );
    }

    private static EventGenerationModel CreateEventGenerationModel(
        EventContext context,
        ImmutableDictionary<ITypeParameterSymbol, string> genericParameterMap
    )
    {
        var methodSymbol = context.TypeSymbol.DelegateInvokeMethod!;
        var parameters = new List<string>();
        var arguments = new List<string>();

        foreach (var parameterSymbol in methodSymbol.Parameters)
        {
            var (type, name) = GetParameterString(
                parameterSymbol,
                genericParameterMap,
                includeModifier: true
            );

            parameters.Add($"{type} {name}");
            arguments.Add(GetArgumentString(parameterSymbol, includeModifier: true));
        }

        if (!methodSymbol.ReturnsVoid)
        {
            var returnType = GetTypeString(methodSymbol.ReturnType, genericParameterMap);

            if (!returnType.EndsWith("?"))
            {
                returnType += "?";
            }

            parameters.Add($"out {returnType} @return");
        }

        return new EventGenerationModel(
            Type: context.Type,
            Name: context.Name,
            UniqueName: context.UniqueName,
            DispatcherParameters: string.Join(", ", parameters),
            DispatcherArguments: string.Join(", ", arguments),
            ReturnsVoid: methodSymbol.ReturnsVoid
        );
    }

    private static PropertyGenerationModel CreatePropertyGenerationModel(PropertyContext context)
    {
        return new PropertyGenerationModel(
            IsIndexer: context.IsIndexer,
            Type: context.Type,
            Name: context.Name,
            ApiName: context.ApiName,
            Parameters: context.Parameters,
            Arguments: context.Arguments,
            GetterDelegateType: context.GetterDelegateType,
            SetterDelegateType: context.SetterDelegateType,
            GetterName: context.GetterName,
            SetterName: context.SetterName,
            GetterParameterName: context.GetterParameterName,
            SetterParameterName: context.SetterParameterName,
            GetterFieldName: context.GetterFieldName,
            SetterFieldName: context.SetterFieldName,
            HasParameters: context.ParameterSymbols.Length > 0
        );
    }

    private static MethodGenerationModel CreateMethodGenerationModel(MethodContext context)
    {
        return new MethodGenerationModel(
            ReturnType: context.ReturnType,
            Parameters: context.Parameters,
            Arguments: context.Arguments,
            DelegateType: context.DelegateType,
            Name: context.Name,
            UniqueName: context.UniqueName,
            ParameterName: context.ParameterName,
            FieldName: context.FieldName
        );
    }

    private static string GetNamespaceString(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? $".{ns.ToDisplayString()}" : "";
    }

    private static INamedTypeSymbol? GetContainingType(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.ContainingType?.ConstructedFrom ?? typeSymbol.ContainingType;
    }

    private static string GetTypeName(INamedTypeSymbol typeSymbol)
    {
        return $"{typeSymbol.Name}{(typeSymbol.Arity > 0 ? $"_{typeSymbol.Arity}" : "")}";
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
    #endregion
}
