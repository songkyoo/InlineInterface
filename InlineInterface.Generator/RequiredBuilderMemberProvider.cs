using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.InlineInterface;

public sealed class RequiredBuilderMemberProvider
{
    private const int CacheLockCancellationPollingMilliseconds = 10;

    private readonly ConcurrentDictionary<INamedTypeSymbol, CacheEntry> _cache = new(
        SymbolEqualityComparer.Default
    );
    private ConcurrentDictionary<ISymbol, string>? _descriptionCache;

    public bool TryGetRequiredMembers(
        INamedTypeSymbol interfaceSymbol,
        CancellationToken cancellationToken,
        out ImmutableArray<RequiredBuilderMember> members
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entry = _cache.GetOrAdd(
            interfaceSymbol,
            static symbol => new CacheEntry(symbol)
        );
        var result = entry.GetResult(cancellationToken);

        members = result.Members;

        return result.IsValid;
    }

    public string GetDescription(
        RequiredBuilderMember member,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cache = Volatile.Read(ref _descriptionCache);

        if (cache is null)
        {
            var created = new ConcurrentDictionary<ISymbol, string>(SymbolEqualityComparer.Default);
            cache = Interlocked.CompareExchange(ref _descriptionCache, created, null) ?? created;
        }

        if (cache.TryGetValue(member.Symbol, out var description))
        {
            return description;
        }

        var createdDescription = member.CreateDescription(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return cache.GetOrAdd(member.Symbol, createdDescription);
    }

    private sealed class CacheEntry(INamedTypeSymbol interfaceSymbol)
    {
        private readonly object _gate = new();
        private RequiredBuilderMembersResult _result;
        private bool _isInitialized;

        public RequiredBuilderMembersResult GetResult(CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _isInitialized))
            {
                return _result;
            }

            var lockTaken = false;

            try
            {
                while (!lockTaken)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Monitor.TryEnter(
                        _gate,
                        CacheLockCancellationPollingMilliseconds,
                        ref lockTaken
                    );
                }

                if (_isInitialized)
                {
                    return _result;
                }

                var result = RequiredBuilderMemberFactory.Create(
                    interfaceSymbol,
                    cancellationToken
                );
                cancellationToken.ThrowIfCancellationRequested();
                _result = result;
                Volatile.Write(ref _isInitialized, true);

                return result;
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_gate);
                }
            }
        }
    }
}

public static class RequiredBuilderMemberFactory
{
    public static RequiredBuilderMembersResult Create(
        INamedTypeSymbol interfaceSymbol,
        CancellationToken cancellationToken
    )
    {
        return InterfaceValidator.ValidateTargetInterface(interfaceSymbol, cancellationToken)
            is TargetInterfaceSymbolValidationResult.Success validationResult
                ? new RequiredBuilderMembersResult(
                    IsValid: true,
                    Members: CreateRequiredMembers(validationResult.Contexts, cancellationToken)
                )
                : new RequiredBuilderMembersResult(
                    IsValid: false,
                    Members: ImmutableArray<RequiredBuilderMember>.Empty
                );
    }

    private static ImmutableArray<RequiredBuilderMember> CreateRequiredMembers(
        ImmutableArray<InterfaceContext> interfaceContexts,
        CancellationToken cancellationToken
    )
    {
        var builder = ImmutableArray.CreateBuilder<RequiredBuilderMember>();
        var methodSignatures = new HashSet<MethodSignature>(MethodSignatureComparer.Instance);
        var propertyMap = new Dictionary<PropertySignature, PropertyRequirement>(
            PropertySignatureComparer.Instance
        );

        foreach (var interfaceContext in interfaceContexts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var methodSymbol in interfaceContext.MethodSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (methodSymbol.MethodKind
                    is MethodKind.EventAdd or MethodKind.EventRemove
                    or MethodKind.PropertyGet or MethodKind.PropertySet
                )
                {
                    continue;
                }

                var methodSignature = MethodSignature.Create(methodSymbol);

                if (methodSignatures.Add(methodSignature))
                {
                    builder.Add(new RequiredBuilderMember(
                        Signature: BuilderMemberSignatureFactory.Create(methodSignature),
                        Symbol: methodSymbol,
                        Kind: RequiredBuilderMemberKind.Method
                    ));
                }
            }

            foreach (var propertySymbol in interfaceContext.PropertySymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var propertySignature = PropertySignature.Create(propertySymbol);

                if (!propertyMap.TryGetValue(propertySignature, out var existing))
                {
                    propertyMap.Add(propertySignature, new PropertyRequirement(
                        Symbol: propertySymbol,
                        RequiresGetter: propertySymbol.GetMethod != null,
                        RequiresSetter: propertySymbol.SetMethod != null
                    ));

                    continue;
                }

                propertyMap[propertySignature] = existing with
                {
                    RequiresGetter = existing.RequiresGetter || propertySymbol.GetMethod != null,
                    RequiresSetter = existing.RequiresSetter || propertySymbol.SetMethod != null,
                };
            }
        }

        foreach (var requirement in propertyMap.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            builder.Add(new RequiredBuilderMember(
                Signature: BuilderMemberSignatureFactory.Create(
                    requirement.Symbol,
                    requirement.RequiresGetter,
                    requirement.RequiresSetter
                ),
                Symbol: requirement.Symbol,
                Kind: requirement.Symbol.IsIndexer
                    ? RequiredBuilderMemberKind.Indexer
                    : RequiredBuilderMemberKind.Property
            ));
        }

        return builder.ToImmutable();
    }

    private readonly record struct PropertyRequirement(
        IPropertySymbol Symbol,
        bool RequiresGetter,
        bool RequiresSetter
    );
}

public readonly record struct RequiredBuilderMembersResult(
    bool IsValid,
    ImmutableArray<RequiredBuilderMember> Members
);

public readonly record struct RequiredBuilderMember(
    BuilderMemberSignature Signature,
    ISymbol Symbol,
    RequiredBuilderMemberKind Kind
)
{
    public string CreateDescription(CancellationToken cancellationToken)
    {
        return this switch
        {
            { Kind: RequiredBuilderMemberKind.Method, Symbol: IMethodSymbol methodSymbol } =>
                $"method '{methodSymbol.Name}({CreateParameterDisplay(methodSymbol.Parameters, cancellationToken)})'",
            { Kind: RequiredBuilderMemberKind.Indexer, Symbol: IPropertySymbol propertySymbol } =>
                $"indexer 'this[{CreateParameterDisplay(propertySymbol.Parameters, cancellationToken)}]'",
            { Kind: RequiredBuilderMemberKind.Property, Symbol: IPropertySymbol propertySymbol } =>
                $"property '{propertySymbol.Name}'",
            _ => throw new InvalidOperationException("Unexpected required builder member."),
        };
    }

    private static string CreateParameterDisplay(
        ImmutableArray<IParameterSymbol> parameters,
        CancellationToken cancellationToken
    )
    {
        var builder = new StringBuilder();

        for (var i = 0; i < parameters.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (i > 0)
            {
                builder.Append(", ");
            }

            var parameter = parameters[i];
            builder.Append(parameter.Type.ToDisplayString(MinimallyQualifiedFormat));
            builder.Append(' ');
            builder.Append(parameter.Name);
        }

        return builder.ToString();
    }
}

public enum RequiredBuilderMemberKind
{
    Method,
    Property,
    Indexer,
}
