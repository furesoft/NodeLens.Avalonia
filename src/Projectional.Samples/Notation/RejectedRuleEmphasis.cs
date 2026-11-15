using Projectional.Core;
using Projectional.Language;

namespace Projectional.Samples;

/// <summary>
/// Strikes through every rule that rejected its request, so the reason a payroll run was refused is
/// visible in the document rather than only in the outcome word at its end.
/// </summary>
/// <remarks>
/// <para>
/// The decoration is a view of the model, not part of the model: nothing is stored, nothing is
/// persisted, and changing an outcome back to <see cref="RuleOutcomes.Accept"/> undoes it on the next
/// rebuild. That is the whole point of doing this outside the notation - the notation says how a rule
/// always reads, this says how a particular rule reads today.
/// </para>
/// <para>
/// Returning the emphasis for the rule node rather than for each of its cells is enough, because a
/// decoration is inherited: the strike applies to the rule's keyword, its name, its condition and the
/// outcome word with one answer.
/// </para>
/// </remarks>
public sealed class RejectedRuleEmphasis : ICellEmphasis
{
    private RejectedRuleEmphasis()
    {
    }

    /// <summary>The one instance: the rule holds no state, so callers need no identity.</summary>
    public static RejectedRuleEmphasis Instance { get; } = new();

    /// <inheritdoc />
    public TextEmphasis EmphasisOf(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Concept.Key == RulesConceptKeys.Rule &&
            node.GetOrNull("outcome") == RuleOutcomes.Reject
                ? TextEmphasis.Strikethrough
                : TextEmphasis.None;
    }

    public override string ToString() => nameof(RejectedRuleEmphasis);
}
