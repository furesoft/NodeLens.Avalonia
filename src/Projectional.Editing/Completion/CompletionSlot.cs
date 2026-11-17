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
internal readonly record struct CompletionSlot(
    Node Owner,
    string Slot,
    PropertyCell? Property,
    ReferenceCell? Reference = null,
    Node? Replace = null)
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
                ? new CompletionSlot(declared, property.Slot, property, Replace: Replaceable(declared))
                : null;
        }

        // A reference cell is always filled - it shows the target's name or a placeholder - so it is not
        // an empty slot. What it offers is the scope's entries, and accepting one re-links the slot.
        if (place.Cell is ReferenceCell reference)
        {
            return context.NodeOf(place.Address) is { } referrer
                ? new CompletionSlot(referrer, reference.Slot, Property: null, reference)
                : null;
        }

        var slot = place.Cell switch
        {
            ChildCell { IsEmpty: true } child => child.Slot,
            CollectionCell { IsEmpty: true } collection => collection.Slot,
            _ => null,
        };

        if (slot is not null && context.NodeOf(place.Address) is { } owner)
        {
            return new CompletionSlot(owner, slot, Property: null);
        }

        // A filled slot produces no caret place of its own, so the caret is somewhere inside the child
        // that fills it. That child is what a list here would replace, which is the one destructive
        // completion - see Replaceable for what it is allowed to be.
        return context.NodeOf(place.Address) is { } inside && Replaceable(inside) is { } replace
            ? new CompletionSlot(replace.Parent!, replace.ParentSlot!, Property: null, Replace: replace)
            : null;
    }

    /// <summary>
    /// <paramref name="node"/>, when its place could be taken by another concept, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two conditions, and the second is the whole safety argument. The node must fill a
    /// <see cref="Cardinality.One"/> slot - replacing anything else would have to choose between siblings -
    /// and it must have <em>no children</em>. A leaf is what a user means when they type <c>1</c> and then
    /// decide they wanted a name: what is lost is the text on screen, and one undo step brings it back. A
    /// node with a subtree is not offered at all, because a single Enter key would then discard work the
    /// user cannot see from the caret - that stays with the intention catalogue's destructive
    /// <c>Replace with "Y"</c>, which a reader chose from a labelled menu.
    /// </para>
    /// <para>
    /// The document root has no place to take, so it is never offered - which is also what keeps the node
    /// the editor is built from out of reach.
    /// </para>
    /// </remarks>
    private static Node? Replaceable(Node node)
    {
        if (node.Parent is not { } parent
            || node.ParentSlot is not { } slot
            || parent.Concept.FindChild(slot) is not { Cardinality: Cardinality.One }
            || node.ChildrenInDocumentOrder().Any())
        {
            return null;
        }

        return node;
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

    /// <summary>Whether this slot is a reference, which is answered from the scope rather than the registry.</summary>
    internal bool IsReference => Reference is not null;

    /// <summary>Whether accepting a proposal here would replace a node rather than fill an empty place.</summary>
    internal bool IsReplacement => Replace is not null;
}
