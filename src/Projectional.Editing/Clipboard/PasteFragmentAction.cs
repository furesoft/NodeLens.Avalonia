using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Puts the fragment on the clipboard into the model, where the caret says it goes.
/// </summary>
/// <remarks>
/// <para>
/// Pasting structure is not typing: it creates the nodes the fragment holds, with new identities, in one undo
/// step. The place it goes is decided from the caret and never replaced by a guess:
/// </para>
/// <list type="bullet">
/// <item>an <b>empty slot</b> takes the fragment - that is what an empty slot is for;</item>
/// <item>a node in a <b>list</b> takes it directly after itself, which is where "paste another one here"
/// means;</item>
/// <item>a node in a <b>single-valued slot</b> has no room, so the paste is refused with a reason instead of
/// replacing what is there - replacing is a labelled entry (<c>Replace with "X"</c>), not a paste;</item>
/// <item>a root has no place at all, so nothing happens.</item>
/// </list>
/// <para>
/// Everything the fragment would need is checked before anything is created: the language has to declare every
/// concept it names, and each root has to conform to the slot and be allowed there. A fragment from another
/// language is the ordinary case, and refusing it by naming the first concept the language does not know is
/// more useful than a tree that half-exists.
/// </para>
/// </remarks>
public sealed class PasteFragmentAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// The caret lands on the first pasted node, so the user sees what arrived and can type into it - the same
    /// landing <see cref="InsertNodeAction"/> uses for what it creates.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.Clipboard.Fragment is not { IsEmpty: false } fragment)
        {
            return EditResult.Unchanged(selection);
        }

        if (fragment.MissingConcept(context.Concepts) is { } missing)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{missing}' is not part of this document's language, so the copied nodes cannot be pasted here.");
        }

        if (!TryPlace(context, out var place, out var reason))
        {
            return EditResult.Rejected(selection, reason);
        }

        var target = place.Parent.Concept.FindChild(place.Slot);

        foreach (var root in fragment.Roots)
        {
            if (!context.Concepts.TryGet(root.ConceptKey, out var concept))
            {
                return EditResult.Rejected(
                    selection,
                    $"Concept '{root.ConceptKey}' is not part of this document's language.");
            }

            if (target is not null && !concept.IsKindOf(target.ChildConceptKey))
            {
                return EditResult.Rejected(
                    selection,
                    $"A '{root.ConceptKey}' cannot stand in slot '{place.Slot}' of '{place.Parent.Concept.Key}'.");
            }

            if (!concept.Applicability.Allows(place.Parent.Concept, place.Slot))
            {
                return EditResult.Rejected(
                    selection,
                    $"A '{root.ConceptKey}' may not stand in slot '{place.Slot}' of '{place.Parent.Concept.Key}'.");
            }
        }

        context.InsertFragment(fragment, place.Parent, place.Slot, place.Index);

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(place.Parent.Id, place.Slot, place.Index)));
    }

    /// <summary>Never coalesces, so a paste is one undo step of its own.</summary>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return null;
    }

    /// <summary>Where the fragment goes, decided from the caret.</summary>
    /// <param name="context">The apply being run.</param>
    /// <param name="place">The parent, slot and index the fragment is inserted at.</param>
    /// <param name="reason">Why there is no place, when there is none.</param>
    /// <returns>Whether the caret names a place a fragment can go.</returns>
    private static bool TryPlace(EditContext context, out PastePlace place, out string reason)
    {
        place = default;
        reason = string.Empty;

        if (context.PlaceAt(context.Selection.Caret) is { IsEmptySlot: true } empty
            && context.NodeOf(empty.Address) is { } owner
            && empty.Address.Slot is { } slot)
        {
            place = new PastePlace(owner, slot, owner.GetChildren(slot).Count);

            return true;
        }

        if (context.NodeOf(context.Selection.Caret.Address) is not { Parent: { } parent, ParentSlot: { } name } node)
        {
            reason = "There is nothing here to paste next to.";

            return false;
        }

        // A single-valued slot has no room for another child, and a paste that replaced one would be a
        // replacement, not an insertion - the labelled entry is where that decision belongs.
        if (parent.Concept.FindChild(name) is not { Cardinality: Cardinality.Many })
        {
            reason = $"Slot '{name}' of '{parent.Concept.Key}' holds one child, so nothing can be pasted beside it.";

            return false;
        }

        place = new PastePlace(parent, name, node.IndexInParent + 1);

        return true;
    }

    public override string ToString() => "paste fragment";
}
