using Projectional.Core;
using Projectional.Editing;
using Projectional.Projection;

namespace Projectional.Intentions;

/// <summary>The entries every language gets for free: rename, wrap in something, reset to the default.</summary>
/// <remarks>
/// <para>
/// A language includes these by registering the provider - one call per session, beside the intentions the language
/// itself declares. They are generic because everything they need is the <em>shape</em> of the place the caret
/// stands on: a node with a name slot, a node some concept could surround, a property holding something other than
/// its declared default. Nothing here knows what a rule or a loop means, which is exactly why it can be a package
/// rather than a per-language rewrite.
/// </para>
/// <para>
/// The entries are derived per caret, never cached, and each one builds its action when it runs
/// (<see cref="Intention.Create"/>) against the model the caret is in - so an offer cannot land on a document that
/// has moved on since the list was shown.
/// </para>
/// <para>
/// The wrap entries are why this catalogue exists. "Surround with <c>if</c>" in a code DSL, "put parentheses around
/// it" in an expression - they are not a fixed list of refactorings here but one search: any concrete concept with
/// a single-child slot that accepts this node's concept and can itself stand in this node's place is a wrapper, and
/// the language says which those are by declaring its slots. A language that adds a grouping concept therefore
/// gains a gesture without writing one.
/// </para>
/// </remarks>
public sealed class StandardIntentions : IIntentionProvider
{
    /// <summary>The stable id of the rename entry (`INT001`).</summary>
    public const string RenameId = "standard.rename";

    /// <summary>The stable id of the reset entry (`INT020`).</summary>
    public const string ResetId = "standard.reset";

    /// <summary>
    /// What every wrap id starts with (`INT006`); the concept's own key follows, so one concept is one id.
    /// </summary>
    /// <remarks>
    /// Per concept rather than one id for the operation, because a list keyed by id would otherwise hold one wrap
    /// entry: <c>Wrap in "If"</c> and <c>Wrap in "Parenthesized"</c> are two different edits, and the de-duplication
    /// in <see cref="IntentionList"/> drops all but the first of an id.
    /// </remarks>
    public const string WrapPrefix = "standard.wrap.";

    /// <summary>The provider a host registers with <c>session.AddIntentions(...)</c>.</summary>
    public static StandardIntentions Provider { get; } = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<Intention> entries = [Rename(context)];

        if (context.Node is { } node)
        {
            foreach (var candidate in WrapperConcepts.Of(node, context.Concepts))
            {
                entries.Add(Wrap(candidate));
            }
        }

        entries.Add(Reset(context));

        return entries;
    }

    /// <summary>The entry that drops the caret into the name slot with the value selected.</summary>
    /// <remarks>
    /// It is absent on a node with no name, and on a name the user has already selected - the second is the
    /// catalogue's own rule that an intention which would change nothing is not offered, and "nothing" here
    /// includes the drawing.
    /// </remarks>
    private static Intention Rename(IntentionContext context)
    {
        var wanted = context.Node is { } node && node.Concept.NameSlot is { } slot
            ? CellAddress.OfSlot(node.Id, slot.Name)
            : (CellAddress?)null;

        var applies = wanted is { } address && !IsCovered(context, address);

        return new Intention(
            RenameId,
            "Rename",
            "Puts the caret in the name so you can type over it.",
            live => live.Node is { } node && node.Concept.NameSlot is { } slot
                ? new SelectSlotAction(CellAddress.OfSlot(node.Id, slot.Name))
                : null,
            availability: applies ? IntentionAvailability.Applies : IntentionAvailability.Absent);
    }

    /// <summary>The entry that writes a property's declared default over whatever is in it.</summary>
    /// <remarks>
    /// The label names the value, because "reset to what?" is the first question a user asks and the answer is
    /// declared rather than typed. Absent when the property already holds its default (including a slot with no
    /// declared one), which is the same rule as <see cref="Rename"/>.
    /// </remarks>
    private static Intention Reset(IntentionContext context)
    {
        var target = ResetFor(context);

        return new Intention(
            ResetId,
            target is { } named ? $"Reset to \"{named.Default}\"" : "Reset to the default",
            target is { } declared
                ? $"Writes \"{declared.Default}\", the value this {declared.Slot} is declared with."
                : "Writes the value this property is declared with over what is there now.",
            live => ResetFor(live) is { } now ? new SetPropertyAction(now.Cell.Address, now.Default) : null,
            availability: target is null ? IntentionAvailability.Absent : IntentionAvailability.Applies);
    }

    /// <summary>One entry per concept that could surround the node the caret stands on.</summary>
    /// <param name="candidate">A concept and the slot that would receive the node.</param>
    private static Intention Wrap(WrapperCandidate candidate)
    {
        var concept = candidate.Wrapper.Key;
        var slot = candidate.Slot.Name;
        var name = ConceptNames.Of(concept);

        return new Intention(
            WrapPrefix + concept,
            $"Wrap in \"{name}\"",
            $"Puts this node inside the \"{slot}\" of a new {name}.",
            live => live.Node is { } node ? new WrapInAction(node.Id, concept, slot) : null);
    }

    /// <summary>The property the caret is in, when it declares a default the value does not already have.</summary>
    /// <remarks>
    /// Asked of the cell rather than of the node, because a value is what the user sees: a property that no notation
    /// draws has no cell, and resetting it is the collapsed-node problem <c>SetNodePropertyAction</c> exists for -
    /// a different entry, not this one.
    /// </remarks>
    private static (PropertyCell Cell, PropertySlot Slot, string Default)? ResetFor(IntentionContext context)
    {
        if (context.Place is not { Cell: PropertyCell cell } || context.Node is not { } node)
        {
            return null;
        }

        if (node.Concept.FindProperty(cell.Slot) is not { DefaultValue: { } fallback } slot)
        {
            return null;
        }

        return string.Equals(cell.Text, fallback, StringComparison.Ordinal) ? null : (cell, slot, fallback);
    }

    /// <summary>Whether <paramref name="address"/> is exactly what the selection covers.</summary>
    private static bool IsCovered(IntentionContext context, CellAddress address) =>
        context.Selection is { HasText: true } selection
        && selection.Anchor.Address.Equals(address)
        && selection.Caret.Address.Equals(address);
}
