using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The empty slot the caret stands in, and the node that declares it.
/// </summary>
/// <remarks>
/// Completion and its accepting action have to agree on which slot is being filled, and deriving it
/// twice would let them disagree - a list accepted into a different slot than the one it described is
/// the bug this type makes impossible. Only an empty slot qualifies: a filled one produces no caret
/// place of its own, so completion would have to edit a word instead of filling a slot.
/// </remarks>
internal readonly record struct CompletionSlot(Node Owner, string Slot)
{
    /// <summary>The empty slot at the caret of <paramref name="context"/>, or <see langword="null"/>.</summary>
    internal static CompletionSlot? At(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PlaceAt(context.Selection.Caret) is not { } place)
        {
            return null;
        }

        var slot = place.Cell switch
        {
            ChildCell { IsEmpty: true } child => child.Slot,
            CollectionCell { IsEmpty: true } collection => collection.Slot,
            _ => null,
        };

        return slot is not null && context.NodeOf(place.Address) is { } owner
            ? new CompletionSlot(owner, slot)
            : null;
    }
}
