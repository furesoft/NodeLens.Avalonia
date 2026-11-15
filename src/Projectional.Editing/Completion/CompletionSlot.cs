using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// What the caret is standing in and could be completed into: an empty slot waiting for a node, or a
/// property cell whose type spells out its legal values.
/// </summary>
/// <remarks>
/// <para>
/// Completion and its accepting action have to agree on what is being completed, and deriving it twice
/// would let them disagree - a list accepted into a different place than the one it described is the bug
/// this type makes impossible. That is why both cases share one derivation instead of one lookup per kind
/// of completion.
/// </para>
/// <para>
/// Only an empty slot qualifies as a node slot: a filled one produces no caret place of its own, so
/// filling it again would have to add a sibling instead of replacing anything. A property is the
/// opposite - it is always filled or empty, and accepting a value writes over whatever is there.
/// </para>
/// <para>
/// <see cref="Property"/> is the cell the value has to be written to, not just its type, because a
/// concept may draw the same property twice and the write belongs to the cell the user is looking at.
/// <see cref="EditContext.PlaceAt"/> resolves the document-order hint, so the cell here is that cell.
/// </para>
/// </remarks>
internal readonly record struct CompletionSlot(Node Owner, string Slot, PropertyCell? Property)
{
    /// <summary>What the caret of <paramref name="context"/> could be completed with, or <see langword="null"/>.</summary>
    internal static CompletionSlot? At(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PlaceAt(context.Selection.Caret) is not { } place)
        {
            return null;
        }

        if (place.Cell is PropertyCell property)
        {
            return context.NodeOf(place.Address) is { } declared
                ? new CompletionSlot(declared, property.Slot, property)
                : null;
        }

        var slot = place.Cell switch
        {
            ChildCell { IsEmpty: true } child => child.Slot,
            CollectionCell { IsEmpty: true } collection => collection.Slot,
            _ => null,
        };

        return slot is not null && context.NodeOf(place.Address) is { } owner
            ? new CompletionSlot(owner, slot, Property: null)
            : null;
    }

    /// <summary>
    /// Whether this slot has anything to offer, which is a question about the slot rather than the caret.
    /// </summary>
    /// <remarks>
    /// An empty child or collection slot always has concepts to propose; a property only does when its
    /// type names its values. Asking this before building a list keeps "a property with nothing to
    /// complete" from becoming a special case at every call site.
    /// </remarks>
    internal bool HasProposals => Property is { } property ? property.Type.Symbols.Count > 0 : true;
}
