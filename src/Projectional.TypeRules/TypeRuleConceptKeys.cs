namespace Projectional.TypeRules;

/// <summary>The keys of the type-rule language: the concepts a type rule is written with.</summary>
/// <remarks>
/// One root and five concepts, and the small number is the design. A type rule says what a concept
/// evaluates to - a fixed type, the type of one of its children, or a choice between those two decided by
/// a property - and nothing about how it is computed. That is the declarative subset of MPS' typesystem:
/// enough to state the rules a DSL actually has, and not a programming language.
/// </remarks>
public static class TypeRuleConceptKeys
{
    /// <summary>The document: the rules of one language.</summary>
    public const string Rules = "TypeRule.Rules";

    /// <summary>One rule: the concept it is about and what that concept evaluates to.</summary>
    public const string Rule = "TypeRule.Rule";

    /// <summary>What a rule answers, abstract because a body is always one of the three kinds.</summary>
    public const string Body = "TypeRule.Body";

    /// <summary>A type named outright.</summary>
    public const string Fixed = "TypeRule.Fixed";

    /// <summary>The type of one of the node's children.</summary>
    public const string Child = "TypeRule.Child";

    /// <summary>A choice between bodies, decided by a property's value.</summary>
    public const string Switch = "TypeRule.Switch";

    /// <summary>One value of a switch, and the body it selects.</summary>
    public const string Case = "TypeRule.Case";
}
