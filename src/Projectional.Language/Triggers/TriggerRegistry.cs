using System.Collections.Immutable;
using Projectional.Core;

namespace Projectional.Language;

/// <summary>
/// An <see cref="ITriggerRules"/> that keeps the triggers a language declared, as an immutable value.
/// </summary>
/// <remarks>
/// <para>
/// Immutable for the reason <see cref="TypingRuleRegistry"/> is: a language declares its triggers once and
/// hands the same registry to every session it opens. <see cref="Add"/> returns a new registry, so declaring
/// a language reads as one expression and the order the calls are written in is the order the triggers are
/// asked in.
/// </para>
/// <para>
/// That order is not decoration: an expression can be reached by more than one trigger - <c>-</c> in front of
/// a condition is a negation, while <c>-</c> after a number is subtraction - and where two of them answer the
/// same side, the earlier-declared one wins. So a language that means both declares the narrower first.
/// </para>
/// </remarks>
public sealed class TriggerRegistry : ITriggerRules
{
    private readonly ImmutableArray<ConceptTrigger> _triggers;

    private TriggerRegistry(ImmutableArray<ConceptTrigger> triggers) => _triggers = triggers;

    /// <summary>A registry with no triggers, so typing at a node's edge leaves the model alone.</summary>
    public static TriggerRegistry Empty { get; } = new(ImmutableArray<ConceptTrigger>.Empty);

    /// <inheritdoc/>
    public bool IsEmpty => _triggers.IsDefaultOrEmpty;

    /// <summary>This registry, with <paramref name="trigger"/> declared last.</summary>
    /// <param name="trigger">The trigger to add.</param>
    /// <returns>A new registry; this one is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="trigger"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Two triggers may share an anchor concept, a side and even a text: an expression reached by <c>-</c> can
    /// be a negation on the left and a subtraction on the right, and a language that wants three different
    /// wrappers for three operators declares three triggers. What it must not do is rely on a tie being broken
    /// any way other than declaration order.
    /// </remarks>
    public TriggerRegistry Add(ConceptTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        return new TriggerRegistry(_triggers.Add(trigger));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ConceptTrigger> Find(Concept anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (IsEmpty)
        {
            return [];
        }

        var matching = new List<ConceptTrigger>();

        foreach (var trigger in _triggers)
        {
            if (anchor.IsKindOf(trigger.AnchorConcept))
            {
                matching.Add(trigger);
            }
        }

        return matching;
    }

    public override string ToString() =>
        IsEmpty
            ? "no triggers"
            : string.Join(", ", _triggers.Select(trigger => trigger.ToString()));
}
