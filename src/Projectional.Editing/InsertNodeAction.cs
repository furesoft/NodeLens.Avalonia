using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Enter: fills the empty child slot the caret is in, or adds a sibling to the list slot the caret's
/// node is in.
/// </summary>
/// <remarks>
/// <para>
/// The two behaviours are one action because they are one key and one intention - "put another thing
/// here" - and because which of them applies is a fact about where the caret is, not about the key. In
/// an empty slot Enter creates the slot's concept; inside a node that sits in a list, Enter creates
/// another element of that list, which is what makes a body grow a step at a time.
/// </para>
/// <para>
/// The caret ends up inside what was created, so pressing Enter twice adds two children rather than
/// re-entering the same slot. It is aimed at the new child's own cell address, which is not a caret
/// stop, and the session resolves it to the first stop inside the new subtree that can be typed into:
/// the action says what it created, and the tree says where in it the user can type. That is why
/// pressing Enter and then typing a name lands in the name instead of on the keyword in front of it.
/// </para>
/// <para>
/// A single-valued slot that already has its child and a node that is a root have nothing to add. Enter
/// there leaves the model alone instead of guessing that the user wanted a new sibling for a concept
/// whose notation does not show a parent list.
/// </para>
/// </remarks>
public sealed class InsertNodeAction : EditAction
{
    /// <inheritdoc/>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.PlaceAt(selection.Caret) is not { } place)
        {
            return EditResult.Unchanged(selection);
        }

        return place.Cell switch
        {
            ChildCell child => FillSlot(context, selection, place, child.Slot),
            CollectionCell collection => FillSlot(context, selection, place, collection.Slot),
            _ => AddSibling(context, selection),
        };
    }

    /// <summary>Creates a node for the empty slot, choosing a concept that can actually be instantiated.</summary>
    /// <remarks>
    /// A slot declared over an abstract concept names a set rather than a node, so the concept that
    /// ends up in it is the language's default for that set. A slot whose declared concept and every
    /// concept below it is abstract has nothing to create, and Enter leaves the model alone rather than
    /// inventing a node the language said cannot exist.
    /// </remarks>
    private static EditResult FillSlot(EditContext context, Selection selection, CaretPlace place, string slot)
    {
        if (context.NodeOf(place.Address) is not { } owner
            || owner.Concept.FindChild(slot) is not { } declared
            || context.Concepts.DefaultInstantiableFor(declared.ChildConceptKey) is not { } concept)
        {
            return EditResult.Unchanged(selection);
        }

        var created = context.AddChild(owner, slot, concept.Key);

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(owner.Id, slot, created.IndexInParent)));
    }

    /// <summary>Adds another element to the list slot the caret's node sits in.</summary>
    private static EditResult AddSibling(EditContext context, Selection selection)
    {
        if (context.NodeOf(selection.Caret.Address) is not { } node
            || node.Parent is not { } parent
            || node.ParentSlot is not { } slot
            || parent.Concept.FindChild(slot) is not { Cardinality: Cardinality.Many })
        {
            return EditResult.Unchanged(selection);
        }

        var created = context.AddChild(parent, slot, node.Concept.Key);

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(parent.Id, slot, created.IndexInParent)));
    }

    public override string ToString() => "insert node";
}
