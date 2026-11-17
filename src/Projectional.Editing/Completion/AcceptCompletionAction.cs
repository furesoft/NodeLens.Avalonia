using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Editing;

/// <summary>
/// Enter or Tab while a completion list is open: creates the highlighted concept in the slot, or writes the
/// highlighted value into the property cell.
/// </summary>
/// <remarks>
/// <para>
/// This is the only completion gesture that changes the model, and it changes it the way the plain key
/// would: for a slot, one child in it and the caret resolved into what was created; for a property, the
/// value written in place of the old one and the caret left where it is, because the user is still looking
/// at the cell they just filled. Either way accepting is one undoable step.
/// </para>
/// <para>
/// The place and the proposal are both re-derived when the key is pressed rather than trusted from the
/// list, because the list survives between keystrokes and the model does not - an undo, a variant switch
/// or a reload can leave a list naming a place that no longer exists. A proposal the place no longer
/// accepts closes the list instead of writing something the language would reject.
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
            || CompletionSlot.At(context) is not { } slot)
        {
            context.Completion = null;

            return EditResult.Unchanged(selection);
        }

        if (slot.Property is { } property)
        {
            context.Completion = null;

            return Accepts(property, item) && context.SetProperty(property, item.ConceptKey)
                ? EditResult.Applied(selection)
                : EditResult.Unchanged(selection);
        }

        if (slot.Reference is { } reference)
        {
            context.Completion = null;

            // The name is re-resolved against the scope as it is now, not trusted from the list: a list
            // survives between keystrokes and the document does not. A name that has become ambiguous is
            // refused rather than picked, which is the rule the scope model states.
            var resolution = context.Scopes.ScopeOf(slot.Owner, reference.Slot).Resolve(item.ConceptKey);

            if (item.Kind != CompletionKind.Reference
                || resolution.Kind != ScopeResolutionKind.Found
                || slot.Owner.Model.Project.FindNode(resolution.Entry.Node) is not { } target
                || !context.SetReference(slot.Owner, reference.Slot, target))
            {
                return EditResult.Unchanged(selection);
            }

            return EditResult.Applied(selection);
        }

        if (!Accepts(context, slot, item))
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

    /// <summary>Whether the property still allows <paramref name="item"/> as one of its values.</summary>
    /// <remarks>
    /// The cell's own parse is the authority, not the list: a type that has been re-declared since the list
    /// opened (a reload, a variant switch) must not be written a symbol it no longer spells.
    /// </remarks>
    private static bool Accepts(PropertyCell property, CompletionItem item) =>
        item.Kind == CompletionKind.Value && property.Type.IsValid(item.ConceptKey);

    public override string ToString() => "accept completion";
}
