using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Swaps the node for a new node of another concept, at the same place.</summary>
/// <remarks>
/// <para>
/// The one destructive substitution in the catalogue: what the old node held is gone, and what stands there is an
/// empty node of the concept the user picked. That is not a shortcut - no declaration says which slot of one concept
/// corresponds to which slot of another - and the model performs it as one transaction so undo puts the old subtree
/// back with its own identities (`Model.Replace`).
/// </para>
/// <para>
/// It is addressed by node id, like the other structural entries, because a menu names what it replaces rather than
/// reading whatever the caret happens to be on when the gesture arrives. Everything a placement can refuse is
/// checked here first and answered as a reason, so an offer that has gone stale declines instead of throwing.
/// </para>
/// <para>
/// A model root is refused with a reason even though the model can replace one: the document a session draws
/// <em>is</em> a root, so replacing that root would take away the node the editor is built from. Deleting a root is
/// refused for the same reason.
/// </para>
/// </remarks>
public sealed class ReplaceNodeAction : EditAction
{
    private readonly NodeId _node;
    private readonly string _concept;

    /// <summary>Replaces <paramref name="node"/> with an empty node of <paramref name="conceptKey"/>.</summary>
    /// <param name="node">The node whose place is taken.</param>
    /// <param name="conceptKey">The concept of the node that takes its place.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="node"/> is empty or <paramref name="conceptKey"/> is not.
    /// </exception>
    public ReplaceNodeAction(NodeId node, string conceptKey)
    {
        if (node.IsEmpty)
        {
            throw new ArgumentException("A replacement needs a node.", nameof(node));
        }

        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        _node = node;
        _concept = conceptKey;
    }

    /// <summary>The node whose place is taken.</summary>
    public NodeId Node => _node;

    /// <summary>The concept of the node that takes its place.</summary>
    public string ConceptKey => _concept;

    /// <inheritdoc/>
    /// <remarks>
    /// What replaced the node is something to name, exactly like a node created by <c>Enter</c>: the user asked for
    /// "a rule instead of this step" and the next thing they do is write the rule's name.
    /// </remarks>
    public override bool SelectsLandedValue => true;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this entry replaces is no longer in the document.");
        }

        if (node.Parent is not { } parent || node.ParentSlot is not { } slot)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' is the document itself, so there is no place to put another node in its stead.");
        }

        if (!context.Concepts.TryGet(_concept, out var concept))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_concept}' is not part of the language of this document.");
        }

        if (concept.IsAbstract)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_concept}' is abstract, so no node can be an instance of it.");
        }

        if (parent.Concept.FindChild(slot) is not { } place || !concept.IsKindOf(place.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_concept}' cannot stand where '{node.Concept.Key}' stands.");
        }

        if (!concept.Applicability.Allows(parent.Concept, slot))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_concept}' may not stand in slot '{slot}'; it may stand in {concept.Applicability}.");
        }

        var created = context.ReplaceChild(node, _concept);

        return EditResult.Applied(
            Selection.At(CellAddress.OfElement(parent.Id, slot, created.IndexInParent)));
    }

    public override string ToString() => $"replace {_node} with {_concept}";
}
