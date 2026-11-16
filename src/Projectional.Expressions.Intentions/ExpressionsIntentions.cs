using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;

namespace Projectional.Expressions.Intentions;

/// <summary>The two intentions the shared expression vocabulary ships with.</summary>
/// <remarks>
/// <para>
/// "Wrap in parentheses" and "negate" are what an expression language can offer at almost any expression,
/// and neither needs to know what the host is for. They live in a package beside the language rather than
/// inside it, for the reason <c>Projectional.Samples.Checks</c> gives: an intention produces an
/// <see cref="EditAction"/>, so it needs the editing layer, and a language package that dragged the editor in
/// could no longer be used headlessly.
/// </para>
/// <para>
/// The entries are declared once and filtered per caret. Each is <see cref="IntentionAvailability.Absent"/>
/// where it would mean nothing - off an expression, or where the expression has no place the wrapper could
/// stand - so a host registers the provider once and the list does the rest.
/// </para>
/// </remarks>
public sealed class ExpressionsIntentions : IIntentionProvider
{
    /// <summary>The stable id of "wrap in parentheses".</summary>
    public const string WrapInParenthesesId = "expressions.wrap-in-parens";

    /// <summary>The stable id of "negate".</summary>
    public const string NegateId = "expressions.negate";

    private const string WrapperSlot = "inner";
    private const string OperandSlot = "operand";
    private const string OperatorSlot = "operator";

    private static readonly Intention Wrap = new(
        WrapInParenthesesId,
        "Wrap in parentheses",
        "Surrounds this expression so the grouping is what the notation says rather than what precedence implies.",
        context => context.Node is { } node
            ? new WrapInAction(node.Id, ExpressionsConceptKeys.Parenthesized, WrapperSlot)
            : null);

    private static readonly Intention Negate = new(
        NegateId,
        "Negate",
        "Surrounds this expression with a negation, so it means the opposite of what it says.",
        context => context.Node is { } node
            ? new WrapInAction(
                node.Id,
                ExpressionsConceptKeys.Unary,
                OperandSlot,
                new Dictionary<string, string> { [OperatorSlot] = Negation })
            : null);

    private const string Negation = "!";

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static ExpressionsIntentions Provider { get; } = new();

    /// <summary>Both intentions, available where the caret stands on an expression that can be surrounded.</summary>
    /// <param name="context">The caret to decide against.</param>
    /// <remarks>
    /// An expression that is already grouped offers nothing to wrap, and one that is already negated offers
    /// nothing to negate: an entry that would produce the same drawing again is noise, and returning it
    /// <em>disabled</em> would light the bulb for an operation nobody needs. Both cases are therefore absent.
    /// </remarks>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            Wrap.With(AvailabilityOf(context, ExpressionsConceptKeys.Parenthesized)),
            Negate.With(AvailabilityOf(context, ExpressionsConceptKeys.Unary)),
        ];
    }

    /// <summary>Whether the caret's expression can be surrounded by <paramref name="wrapperConceptKey"/>.</summary>
    private static IntentionAvailability AvailabilityOf(IntentionContext context, string wrapperConceptKey)
    {
        if (context.Node is not { } node || !node.Concept.IsKindOf(ExpressionsConceptKeys.Expression))
        {
            return IntentionAvailability.Absent;
        }

        // Wrapping something that is already that wrapper would add a level without changing the meaning.
        if (node.Concept.IsKindOf(wrapperConceptKey))
        {
            return IntentionAvailability.Absent;
        }

        // The wrapper has to be able to take the node's place, which is a fact about the slot the node sits
        // in. A root expression sits in none, so there is nowhere to put parentheses.
        return node.ParentSlot is { } slot
            && node.Parent?.Concept.FindChild(slot) is { } place
            && context.Concepts.TryGet(wrapperConceptKey, out var wrapper)
            && wrapper.IsKindOf(place.ChildConceptKey)
                ? IntentionAvailability.Applies
                : IntentionAvailability.Absent;
    }
}
