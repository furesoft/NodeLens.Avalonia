namespace Projectional.EvaluationRules;

/// <summary>The keys of the language an evaluation rule is written in, one per concept it declares.</summary>
/// <remarks>
/// A rule is a model, so a language can ship its meaning the way it ships its structure, notation and constraints
/// ([ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md)). The vocabulary is five bodies rather than a
/// statement list, for the reason the type rules are: an imperative typesystem would mean shipping an interpreter for a
/// language whose only job is to answer one question per node, and these five cover what a small vocabulary actually says.
/// </remarks>
public static class EvaluationRuleConceptKeys
{
    /// <summary>One rule: the concept it answers for, and the body that answers.</summary>
    public const string Rule = "Evaluation.Rule";

    /// <summary>What a rule answers. Abstract, because a rule is written as one of the bodies below.</summary>
    public const string Body = "Evaluation.Body";

    /// <summary>The value of one of the node's properties, read through its declared type.</summary>
    public const string Property = "Evaluation.Property";

    /// <summary>The value of the single child in a slot: how parentheses and a function body answer.</summary>
    public const string Child = "Evaluation.Child";

    /// <summary>The value the run bound to the name a property holds.</summary>
    public const string Variable = "Evaluation.Variable";

    /// <summary>An operator applied to the values of one or two children.</summary>
    public const string Operator = "Evaluation.Operator";

    /// <summary>One of two bodies, chosen by the truth value of a child slot's value.</summary>
    public const string Choose = "Evaluation.Choose";
}
