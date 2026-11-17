using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Inserts a new empty node of the same concept right before or right after <paramref name="node"/>.</summary>
/// <remarks>
/// <para>
/// This is <c>Enter</c>'s other half, named rather than pointed: <see cref="InsertNodeAction"/> inserts a sibling of
/// whatever the caret is in, always after it, because that is what the key means. A menu entry can say which side,
/// and it can say it before the user's caret is the thing being acted on - which is why this action carries a node
/// id and a place instead of reading the selection.
/// </para>
/// <para>
/// The concept is the sibling's, not a default: "insert before" next to a step has to make a step, or the user would
/// be inserting something they did not point at. Filling the new node's slots is <see cref="InsertNodeAction"/>'s
/// business, not this one's - a keystroke after it does that through the typing rules, exactly as after Enter.
/// </para>
/// </remarks>
public sealed class InsertSiblingAction : EditAction
{
    private readonly NodeId _node;
    private readonly SiblingPlace _place;

    /// <summary>Inserts an empty sibling of <paramref name="node"/> at <paramref name="place"/>.</summary>
    /// <param name="node">The node to stand beside.</param>
    /// <param name="place">Which side the new node goes on.</param>
    public InsertSiblingAction(NodeId node, SiblingPlace place)
    {
        _node = node;
        _place = place;
    }

    /// <summary>The node whose slot receives the new child.</summary>
    public NodeId Node => _node;

    /// <summary>Which side of it the new node goes on.</summary>
    public SiblingPlace Place => _place;

    /// <inheritdoc />
    public override bool SelectsLandedValue => true;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this entry stands beside is no longer in the document.");
        }

        if (node.Parent is not { } parent || node.ParentSlot is not { } slot)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' is not inside anything, so it has no neighbour to be placed beside.");
        }

        if (parent.Concept.FindChild(slot) is not { Cardinality: Cardinality.Many })
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{slot}' of concept '{parent.Concept.Key}' holds at most one child, so there is no room beside it.");
        }

        var index = node.IndexInParent + (_place == SiblingPlace.After ? 1 : 0);
        var created = context.AddChild(parent, slot, index, node.Concept.Key);

        return EditResult.Applied(Selection.At(CellAddress.OfElement(parent.Id, slot, created.IndexInParent)));
    }

    public override string ToString() =>
        $"insert {(_place == SiblingPlace.Before ? "before" : "after")} {_node}";
}
