using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;
using Projectional.Samples;

namespace Projectional.Samples.Intentions;

/// <summary>The intention the toy rules language ships with: accept the rule under the caret.</summary>
/// <remarks>
/// <para>
/// One entry is enough to show what a language author writes: a declared intention, an availability per
/// caret, and an action built from the model - no plumbing in the host beyond one registration call. The
/// value it writes is the language's own declaration (<see cref="RuleOutcomes.Accept"/>), not a string
/// copied into the package.
/// </para>
/// <para>
/// It lives beside the language rather than inside it for the reason <c>Projectional.Samples.Checks</c>
/// gives: it produces an <see cref="EditAction"/>, so it needs the editing layer.
/// </para>
/// </remarks>
public sealed class RulesIntentions : IIntentionProvider
{
    /// <summary>The stable id of "accept this rule".</summary>
    public const string AcceptId = "rules.outcome.accept";

    private const string OutcomeSlot = "outcome";

    private static readonly Intention Accept = new(
        AcceptId,
        "Accept this rule",
        "Sets the outcome so this rule decides in favour of whatever it matches.",
        context => context.Node is { } node
            ? new SetPropertyAction(CellAddress.OfSlot(node.Id, OutcomeSlot), RuleOutcomes.Accept)
            : null);

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static RulesIntentions Provider { get; } = new();

    /// <summary>The entry, available where the caret stands on a rule that does not already accept.</summary>
    /// <param name="context">The caret to decide against.</param>
    /// <remarks>
    /// A rule that already accepts offers nothing: the action would write the value it already holds, which
    /// is an unchanged edit rather than a result. Reporting it as disabled would light the bulb for
    /// something nobody needs, so it is absent.
    /// </remarks>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var applies = context.Node is { } node
            && node.Concept.IsKindOf(RulesConceptKeys.Rule)
            && !string.Equals(node.GetOrNull(OutcomeSlot), RuleOutcomes.Accept, StringComparison.Ordinal);

        return [Accept.With(applies ? IntentionAvailability.Applies : IntentionAvailability.Absent)];
    }
}
