using Projectional.Core;
using Projectional.Editing;
using Projectional.Expressions;

namespace Projectional.Expressions.Intentions;

/// <summary>The one intention the shared expression vocabulary ships with: negate.</summary>
/// <remarks>
/// <para>
/// "Negate" is what an expression language can offer at almost any expression, and it does not need to know what the
/// host is for. It lives in a package beside the language rather than inside it, for the reason
/// <c>Projectional.Samples.Checks</c> gives: an intention produces an <see cref="EditAction"/>, so it needs the
/// editing layer, and a language package that dragged the editor in could no longer be used headlessly.
/// </para>
/// <para>
/// It is the entry the framework's own catalogue cannot replace. "Wrap in parentheses" used to live here too and is
/// gone, because <c>Wrap in "Parenthesized"</c> is the same edit and the catalogue's search finds it from the
/// declarations - one row per operation is the rule. A negation is not that kind of entry: it is a <c>Unary</c>
/// <em>born with</em> <c>operator = "!"</c>, and no declaration says which of the two unary operators a bare
/// <c>Unary</c> should start with. Until a language can declare intentions of its own (M5), that value has to come
/// from the language, so this entry stays beside the language.
/// </para>
/// <para>
/// The entry is declared once and filtered per caret. It is <see cref="IntentionAvailability.Absent"/> where it would
/// mean nothing - off an expression, where the expression has no place the wrapper could stand, or where it is
/// already a negation - so a host registers the provider once and the list does the rest.
/// </para>
/// </remarks>
public sealed class ExpressionsIntentions : IIntentionProvider
{
    /// <summary>The stable id of "negate".</summary>
    public const string NegateId = "expressions.negate";

    private const string OperandSlot = "operand";
    private const string OperatorSlot = "operator";

    private const string Negation = "!";

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

    /// <summary>The provider a host registers with <c>session.AddIntentions(..., IntentionSource.Language)</c>.</summary>
    public static ExpressionsIntentions Provider { get; } = new();

    /// <summary>The one intention, available where the caret stands on an expression it can surround.</summary>
    /// <param name="context">The caret to decide against.</param>
    /// <remarks>
    /// An expression that is already negated offers nothing to negate: an entry that would produce the same drawing
    /// again is noise, and returning it <em>disabled</em> would light the bulb for an operation nobody needs. That is
    /// why the case is absent.
    /// </remarks>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return [Negate.With(AvailabilityOf(context))];
    }

    /// <summary>Whether the caret's expression can be surrounded by a negation.</summary>
    private static IntentionAvailability AvailabilityOf(IntentionContext context)
    {
        if (context.Node is not { } node || !node.Concept.IsKindOf(ExpressionsConceptKeys.Expression))
        {
            return IntentionAvailability.Absent;
        }

        // Wrapping something that is already that wrapper would add a level without changing the meaning.
        if (node.Concept.IsKindOf(ExpressionsConceptKeys.Unary))
        {
            return IntentionAvailability.Absent;
        }

        // The wrapper has to be able to take the node's place, which is a fact about the slot the node sits
        // in. A root expression sits in none, so there is nowhere to put the negation.
        return node.ParentSlot is { } slot
            && node.Parent?.Concept.FindChild(slot) is { } place
            && context.Concepts.TryGet(ExpressionsConceptKeys.Unary, out var wrapper)
            && wrapper.IsKindOf(place.ChildConceptKey)
                ? IntentionAvailability.Applies
                : IntentionAvailability.Absent;
    }
}
