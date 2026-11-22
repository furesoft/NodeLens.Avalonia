namespace Projectional.EvaluationRules;

/// <summary>The keys of the language an evaluation rule is written in, one per concept it declares.</summary>
/// <remarks>
/// <para>
/// A rule is a model, so a language can ship its meaning the way it ships its structure, notation and constraints
/// ([ADR 0014](../../../docs/adr/0014-both-authoring-paths-are-permanent.md)). The vocabulary is a set of bodies rather than a
/// statement list, for the reason the type rules are: an imperative typesystem would mean shipping an interpreter for a
/// language whose only job is to answer one question per node.
/// </para>
/// <para>
/// What the bodies are chosen to cover is an evaluator for a real DSL, not a demonstration: a value read from the node
/// (<see cref="Property"/>, <see cref="Child"/>, <see cref="Variable"/>), a question about the node's shape
/// (<see cref="Has"/>, <see cref="Count"/>), the value of the node a reference points at (<see cref="Reference"/>), a
/// value written down (<see cref="Constant"/>),
/// arithmetic and logic over other bodies (<see cref="Operator"/>), a choice between two bodies (<see cref="Choose"/>), the
/// first entry of a list that applies followed by what that entry answers (<see cref="First"/>), the total of a list
/// (<see cref="Sum"/>), a record of named values (<see cref="Record"/>) and the values of a slot as a list
/// (<see cref="List"/>), a name bound for the rest
/// of the rule (<see cref="Bind"/>), and a refusal with a sentence (<see cref="Fail"/>).
/// </para>
/// </remarks>
public static class EvaluationRuleConceptKeys
{
    /// <summary>One rule: the concept it answers for, and the body that answers.</summary>
    public const string Rule = "Evaluation.Rule";

    /// <summary>What a rule answers. Abstract, because a rule is written as one of the bodies below.</summary>
    public const string Body = "Evaluation.Body";

    /// <summary>The value of one of the node's properties, read through its declared type.</summary>
    public const string Property = "Evaluation.Property";

    /// <summary>The value of the single child in a slot.</summary>
    public const string Child = "Evaluation.Child";

    /// <summary>Whether a slot of the node holds anything, whatever kind of slot it is.</summary>
    public const string Has = "Evaluation.Has";

    /// <summary>How many entries a child slot holds.</summary>
    public const string Count = "Evaluation.Count";

    /// <summary>The value of the node a reference slot points at.</summary>
    public const string Reference = "Evaluation.Reference";

    /// <summary>The total of what the entries of a slot answer.</summary>
    public const string Sum = "Evaluation.Sum";

    /// <summary>A record value, built out of the fields the rule names.</summary>
    public const string Record = "Evaluation.Record";

    /// <summary>One named value inside a <see cref="Record"/>.</summary>
    public const string RecordField = "Evaluation.RecordField";

    /// <summary>The values of every entry of a child slot, as one list.</summary>
    public const string List = "Evaluation.List";

    /// <summary>One field of a record value the rule names.</summary>
    public const string Field = "Evaluation.Field";

    /// <summary>The value the run bound to the name a property holds.</summary>
    public const string Variable = "Evaluation.Variable";

    /// <summary>A value written into the rule itself.</summary>
    public const string Constant = "Evaluation.Constant";

    /// <summary>An operator applied to the values of two other bodies, or of one for a unary operator.</summary>
    public const string Operator = "Evaluation.Operator";

    /// <summary>One of two bodies, chosen by the truth value of a third.</summary>
    public const string Choose = "Evaluation.Choose";

    /// <summary>The first entry of a list whose condition holds, answered by the body beside the condition.</summary>
    public const string First = "Evaluation.First";

    /// <summary>A name bound to one body while another one answers.</summary>
    public const string Bind = "Evaluation.Bind";

    /// <summary>A refusal: the rule says why it has no value instead of producing one.</summary>
    public const string Fail = "Evaluation.Fail";
}
