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

    /// <summary>The stable id of the duplicate entry (`INT002`).</summary>
    public const string DuplicateId = "standard.duplicate";

    /// <summary>The stable id of the delete entry (`INT003`).</summary>
    public const string DeleteId = "standard.delete";

    /// <summary>The stable id of "move one place towards the front" (`INT004`).</summary>
    public const string MoveUpId = "standard.move-up";

    /// <summary>The stable id of "move one place towards the end" (`INT004`).</summary>
    public const string MoveDownId = "standard.move-down";

    /// <summary>The stable id of "move to the first place" (`INT005`).</summary>
    public const string MoveToFrontId = "standard.move-front";

    /// <summary>The stable id of "move to the last place" (`INT005`).</summary>
    public const string MoveToBackId = "standard.move-back";

    /// <summary>The stable id of "insert an empty sibling before this one" (`INT030`).</summary>
    public const string InsertBeforeId = "standard.insert-before";

    /// <summary>The stable id of "insert an empty sibling after this one" (`INT030`).</summary>
    public const string InsertAfterId = "standard.insert-after";

    /// <summary>The stable id of "sort this list by name" (`INT031`).</summary>
    public const string SortByNameId = "standard.sort";

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

    /// <summary>
    /// What every replace id starts with (`INT007`); the concept's own key follows, so one concept is one id.
    /// </summary>
    /// <remarks>
    /// Per concept for the same reason as <see cref="WrapPrefix"/>: <c>Replace with "IntLiteral"</c> and
    /// <c>Replace with "Name"</c> are two different edits, and a list keyed by id drops all but the first.
    /// </remarks>
    public const string ReplacePrefix = "standard.replace.";

    /// <summary>The provider a host registers with <c>session.AddIntentions(...)</c>.</summary>
    public static StandardIntentions Provider { get; } = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<Intention> At(IntentionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<Intention> entries =
        [
            Rename(context),
            Duplicate(context),
            Delete(context),
            Move(context, MoveUpId, "Move up", "Swaps this node with the one before it.", NodeMove.Up),
            Move(context, MoveDownId, "Move down", "Swaps this node with the one after it.", NodeMove.Down),
            Move(context, MoveToFrontId, "Move to front", "Puts this node first in its list.", NodeMove.ToFront),
            Move(context, MoveToBackId, "Move to back", "Puts this node last in its list.", NodeMove.ToBack),
            Insert(context, InsertBeforeId, "Insert before", SiblingPlace.Before),
            Insert(context, InsertAfterId, "Insert after", SiblingPlace.After),
            Sort(context),
        ];

        if (context.Node is { } node)
        {
            foreach (var candidate in WrapperConcepts.Of(node, context.Concepts))
            {
                entries.Add(Wrap(candidate));
            }

            foreach (var candidate in ReplacementConcepts.Of(node, context.Concepts))
            {
                entries.Add(Replace(candidate));
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

    /// <summary>The entry that copies the node and everything below it into the position after it.</summary>
    /// <remarks>
    /// Offered only where a second sibling can exist: a node without a place has no next position, and the only
    /// child of a single-valued slot has nowhere to be copied to.
    /// </remarks>
    private static Intention Duplicate(IntentionContext context)
    {
        var applies = context.Node is { } node && IsSibling(node);

        return new Intention(
            DuplicateId,
            "Duplicate",
            "Copies this node and everything inside it, right after this one.",
            live => live.Node is { } node && IsSibling(node) ? new DuplicateNodeAction(node.Id) : null,
            availability: applies ? IntentionAvailability.Applies : IntentionAvailability.Absent);
    }

    /// <summary>The entry that removes the node under the caret, exactly as the key does.</summary>
    /// <remarks>
    /// It is marked <see cref="EditSafety.Destructive"/> rather than hidden: a surface may paint a warning, but the
    /// user asking what can be done here is entitled to see the one thing that removes something. A root is not
    /// offered it, because the key does not delete a root either.
    /// </remarks>
    private static Intention Delete(IntentionContext context)
    {
        var applies = context.Node is { Parent: not null };

        return new Intention(
            DeleteId,
            "Delete",
            "Removes this node and everything inside it.",
            live => live.Node is { Parent: not null } ? new DeleteNodeAction() : null,
            safety: EditSafety.Destructive,
            availability: applies ? IntentionAvailability.Applies : IntentionAvailability.Absent);
    }

    /// <summary>The entry that reorders the node one place, or to an end of its list.</summary>
    /// <param name="context">The caret the entry is derived at.</param>
    /// <param name="id">The entry's stable id.</param>
    /// <param name="label">What the entry says.</param>
    /// <param name="description">One sentence saying what it does.</param>
    /// <param name="move">The direction, including the two extremes.</param>
    /// <remarks>
    /// Absent where the move would change nothing - the first node cannot move up, the last cannot move down - which
    /// is the catalogue's rule that an intention which would do nothing is not offered. The action itself also
    /// answers <see cref="EditResult.Unchanged"/> there, because the list may be older than the document.
    /// </remarks>
    private static Intention Move(
        IntentionContext context,
        string id,
        string label,
        string description,
        NodeMove move)
    {
        var applies = CanMove(context.Node, move);

        return new Intention(
            id,
            label,
            description,
            live => CanMove(live.Node, move) ? new MoveNodeAction(move) : null,
            availability: applies ? IntentionAvailability.Applies : IntentionAvailability.Absent);
    }

    /// <summary>The entry that inserts an empty sibling on one side of the node.</summary>
    /// <param name="context">The caret the entry is derived at.</param>
    /// <param name="id">The entry's stable id.</param>
    /// <param name="label">What the entry says.</param>
    /// <param name="place">Which side the new node goes on.</param>
    private static Intention Insert(IntentionContext context, string id, string label, SiblingPlace place)
    {
        var applies = IsSibling(context.Node);
        var side = place == SiblingPlace.Before ? "before" : "after";

        return new Intention(
            id,
            label,
            $"Inserts an empty node of the same concept {side} this one.",
            live => live.Node is { } node && IsSibling(node)
                ? new InsertSiblingAction(node.Id, place)
                : null,
            availability: applies ? IntentionAvailability.Applies : IntentionAvailability.Absent);
    }

    /// <summary>Whether <paramref name="move"/> would take <paramref name="node"/> somewhere else.</summary>
    private static bool CanMove(Node? node, NodeMove move)
    {
        if (node is null || List(node) is not { } siblings)
        {
            return false;
        }

        return move switch
        {
            NodeMove.Up or NodeMove.ToFront => node.IndexInParent > 0,
            _ => node.IndexInParent < siblings.Count - 1,
        };
    }

    /// <summary>The siblings a node stands among, when its slot holds more than one.</summary>
    private static IReadOnlyList<Node>? List(Node node) =>
        node.Parent is { } parent
        && node.ParentSlot is { } slot
        && parent.Concept.FindChild(slot) is { Cardinality: Cardinality.Many }
            ? parent.GetChildren(slot)
            : null;

    /// <summary>Whether <paramref name="node"/> stands in a slot that can hold more than one child.</summary>
    private static bool IsSibling(Node? node) => node is not null && List(node) is not null;

    /// <summary>One entry per concept that could take the place of the node the caret stands on.</summary>
    /// <param name="concept">The concept that would replace it.</param>
    /// <remarks>
    /// The label names the concept and the description says that what is there goes, because this is the one entry in
    /// the catalogue that loses something. It is marked <see cref="EditSafety.Destructive"/> rather than hidden: a
    /// surface should be able to warn, and a user asking what can be done here is entitled to see it.
    /// </remarks>
    private static Intention Replace(Concept concept)
    {
        var key = concept.Key;
        var name = ConceptNames.Of(key);

        return new Intention(
            ReplacePrefix + key,
            $"Replace with \"{name}\"",
            $"Swaps this node for an empty {name}; what it held is gone.",
            live => live.Node is { } node ? new ReplaceNodeAction(node.Id, key) : null,
            safety: EditSafety.Destructive);
    }

    /// <summary>The entry that puts the list the node stands in into name order.</summary>
    /// <remarks>
    /// The list is the one the node is an element of, not one inside it: "sort by name" is something the user asks
    /// while looking at the elements. Absent wherever the sort would move nothing - a list already in order, a list of
    /// one, and a list whose elements declare no name to be sorted by (see <see cref="ChildOrder"/>) - which is the
    /// catalogue's rule that an entry which would change nothing is not offered.
    /// </remarks>
    private static Intention Sort(IntentionContext context)
    {
        var target = Sortable(context);

        return new Intention(
            SortByNameId,
            "Sort by name",
            "Reorders this list by name. The order is part of the document, so it is saved and undone like any edit.",
            live => Sortable(live) is { } now ? new SortChildrenAction(now.Owner, now.Slot) : null,
            safety: EditSafety.Destructive,
            availability: target is null ? IntentionAvailability.Absent : IntentionAvailability.Applies);
    }

    /// <summary>The list the caret's node stands in, when sorting it would move something.</summary>
    private static (NodeId Owner, string Slot)? Sortable(IntentionContext context)
    {
        if (context.Node is not { Parent: { } parent, ParentSlot: { } slot })
        {
            return null;
        }

        return ChildOrder.WouldChange(parent, slot) ? (parent.Id, slot) : null;
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
