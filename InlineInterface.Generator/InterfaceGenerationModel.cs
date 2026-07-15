using System.Collections.Immutable;

namespace Macaron.InlineInterface;

internal sealed record InterfaceGenerationModel(
    string Type,
    string GenericParameters,
    ImmutableArray<string> GenericParameterConstraints,
    string MergedTypePrefix,
    string TypeBuilderNamespace,
    string TypeBuilder,
    string GlobalTypeBuilder,
    ImmutableArray<EventGenerationModel> Events,
    ImmutableArray<EventImplementationModel> EventImplementations,
    ImmutableArray<PropertyGenerationModel> Properties,
    ImmutableArray<PropertyImplementationModel> PropertyImplementations,
    ImmutableArray<MethodGenerationModel> Methods,
    ImmutableArray<MethodImplementationModel> MethodImplementations,
    string HintName
)
{
    #region IEquatable<InterfaceGenerationModel> Interface
    public bool Equals(InterfaceGenerationModel? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        return StringComparer.Ordinal.Equals(Type, other.Type)
            && StringComparer.Ordinal.Equals(GenericParameters, other.GenericParameters)
            && SequenceEqual(GenericParameterConstraints, other.GenericParameterConstraints, StringComparer.Ordinal)
            && StringComparer.Ordinal.Equals(MergedTypePrefix, other.MergedTypePrefix)
            && StringComparer.Ordinal.Equals(TypeBuilderNamespace, other.TypeBuilderNamespace)
            && StringComparer.Ordinal.Equals(TypeBuilder, other.TypeBuilder)
            && StringComparer.Ordinal.Equals(GlobalTypeBuilder, other.GlobalTypeBuilder)
            && SequenceEqual(Events, other.Events, EqualityComparer<EventGenerationModel>.Default)
            && SequenceEqual(
                EventImplementations,
                other.EventImplementations,
                EqualityComparer<EventImplementationModel>.Default
            )
            && SequenceEqual(Properties, other.Properties, EqualityComparer<PropertyGenerationModel>.Default)
            && SequenceEqual(
                PropertyImplementations,
                other.PropertyImplementations,
                EqualityComparer<PropertyImplementationModel>.Default
            )
            && SequenceEqual(Methods, other.Methods, EqualityComparer<MethodGenerationModel>.Default)
            && SequenceEqual(
                MethodImplementations,
                other.MethodImplementations,
                EqualityComparer<MethodImplementationModel>.Default
            )
            && StringComparer.Ordinal.Equals(HintName, other.HintName);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(Type);

            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(GenericParameters);
            hashCode = AddValuesHashCode(hashCode, GenericParameterConstraints, StringComparer.Ordinal);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(MergedTypePrefix);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(TypeBuilderNamespace);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(TypeBuilder);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(GlobalTypeBuilder);
            hashCode = AddValuesHashCode(hashCode, Events, EqualityComparer<EventGenerationModel>.Default);
            hashCode = AddValuesHashCode(
                hashCode,
                EventImplementations,
                EqualityComparer<EventImplementationModel>.Default
            );
            hashCode = AddValuesHashCode(
                hashCode,
                Properties,
                EqualityComparer<PropertyGenerationModel>.Default
            );
            hashCode = AddValuesHashCode(
                hashCode,
                PropertyImplementations,
                EqualityComparer<PropertyImplementationModel>.Default
            );
            hashCode = AddValuesHashCode(hashCode, Methods, EqualityComparer<MethodGenerationModel>.Default);
            hashCode = AddValuesHashCode(
                hashCode,
                MethodImplementations,
                EqualityComparer<MethodImplementationModel>.Default
            );
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(HintName);

            return hashCode;
        }
    }
    #endregion

    #region Static Methods
    private static bool SequenceEqual<T>(ImmutableArray<T> x, ImmutableArray<T> y, IEqualityComparer<T> comparer)
    {
        if (x.IsDefault || y.IsDefault)
        {
            return x.IsDefault == y.IsDefault;
        }

        if (x.Length != y.Length)
        {
            return false;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (!comparer.Equals(x[i], y[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int AddValuesHashCode<T>(int hashCode, ImmutableArray<T> values, IEqualityComparer<T> comparer)
    {
        unchecked
        {
            if (values.IsDefault)
            {
                return (hashCode * 397) ^ -1;
            }

            foreach (var value in values)
            {
                hashCode = (hashCode * 397) ^ comparer.GetHashCode(value);
            }

            return hashCode;
        }
    }
    #endregion
}
