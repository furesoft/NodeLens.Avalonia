using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Enter or Tab while a completion list is open: creates the highlighted concept in the slot.
/// </summary>
/// <remarks>
/// <para>
/// This is the only completion gesture that changes the model, and it changes it the way Enter would:
/// one child in the slot, the caret resolved into what was created. Filling a slot is therefore one
/// undoable step whether the user typed the concept's name into a list or let Enter pick the default.
/// </para>
/// <para>
/// The slot and the proposal are both re-derived when the key is pressed rather than trusted from the
/// list, because the list survives between keystrokes and the model does not - an undo, a variant switch
/// or a reload can leave a list naming a slot that no longer exists. A proposal that the slot no longer
/// accepts closes the list instead of creating a node the language would reject.
/// </para>
/// <para>
/// With nothing matching the filter there is nothing to accept, so the key dismisses the list and leaves
/// the model alone; pressing Enter again then takes the ordinary path and creates the slot's default,
/// which is what the user would have got without a list open.
/// </para>
/// </remarks>
public sealed class AcceptCompletionAction : CompletionAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.Completion is not { Selected: { } item }
            || CompletionSlot.At(context) is not { } slot
            || !Accepts(context, slot, item))
        {
            context.Completion = null;

            return EditResult.Unchanged(selection);
        }

        var created = context.AddChild(slot.Owner, slot.Slot, item.ConceptKey);
        context.Completion = null;

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(slot.Owner.Id, slot.Slot, created.IndexInParent)));
    }

    /// <summary>Whether the slot at the caret still offers <paramref name="item"/>.</summary>
    private static bool Accepts(EditContext context, CompletionSlot slot, CompletionItem item)
    {
        foreach (var proposal in CompletionSource.Proposals(context.Concepts, slot.Owner, slot.Slot))
        {
            if (string.Equals(proposal.ConceptKey, item.ConceptKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public override string ToString() => "accept completion";
}
