using Microsoft.CodeAnalysis;

namespace Macaron.InlineInterface;

public static class InlineInterfaceDiagnostics
{
    public static readonly DiagnosticDescriptor TargetTypeMustBeInterfaceRule = new(
        id: "MII0001",
        title: "Target type must be interface",
        messageFormat: "Type '{0}' is not an interface. Only interface types can be used as inline interface targets.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TargetTypeCannotBeNullableRule = new(
        id: "MII0002",
        title: "Target type cannot be nullable",
        messageFormat: "Type '{0}' is nullable. Nullable interface types are not supported. Use the non-nullable version instead.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NotAllowedGenericMethodRule = new(
        id: "MII0003",
        title: "Generic methods are not allowed",
        messageFormat: "Generic method '{1}' is not allowed in target interface '{0}'. Inline interfaces do not support generic methods.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NotAllowedMethodModifierRule = new(
        id: "MII0004",
        title: "Method parameter modifiers are not allowed",
        messageFormat: "Method '{1}' in target interface '{0}' has unsupported parameter modifiers (ref, out, in, or params). Only value and reference parameters are supported.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnexpectedMemberTypeRule = new(
        id: "MII0005",
        title: "Unexpected member type",
        messageFormat: "Unexpected member '{2}' of type '{1}' found in target interface '{0}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TargetTypeMustBeAccessibleRule = new(
        id: "MII0006",
        title: "Target type must be accessible",
        messageFormat: "Type '{0}' is not accessible from generated code. Target interfaces and all containing types must be public, internal, or protected internal.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NotAllowedEventModifierRule = new(
        id: "MII0007",
        title: "Event delegate parameter modifiers are not allowed",
        messageFormat: "Event '{1}' in target interface '{0}' has unsupported delegate parameter modifiers. Only 'in' is supported for event delegate parameters.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor BuilderMustBeCompletedInSameExpressionRule = new(
        id: "MII0008",
        title: "Inline interface builder must be completed in the same expression",
        messageFormat: "Inline interface builders must stay in a single fluent chain ending with 'Build()'. Do not store the intermediate builder or pass it around.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MissingRequiredBuilderMembersRule = new(
        id: "MII0009",
        title: "Inline interface builder is missing required delegates",
        messageFormat: "Inline interface implementation for '{0}' is missing delegate configuration for {1}. Pass delegates for all required members or set allowMissingImplementation: true.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
