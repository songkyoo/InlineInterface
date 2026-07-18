namespace Macaron.InlineInterface;

public enum InterfaceValidationIssueKind
{
    NotAllowedEventModifier,
    NotAllowedGenericMethod,
    NotAllowedMethodModifier,
    NotAllowedStaticAbstractMember,
    UnexpectedMemberType,
}
