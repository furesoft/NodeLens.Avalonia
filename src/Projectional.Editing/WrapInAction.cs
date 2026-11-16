using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Puts a node inside a newly created wrapper, keeping the node's identity and its subtree.
/// </summary>
/// <remarks>
/// <para>
/// This is the one genuinely new structural edit the quick-fix catalogue needed: "wrap in parentheses" is
/// the repair for an expression whose grouping a reader has to infer, and no combination of the existing
/// actions can express it. A single-valued slot holds at most one child, so creating the wrapper and then
/// moving the node into it would need a moment in which both are in the slot; the model performs the
/// detach-create-attach sequence as one transaction instead (<see cref="Model.Wrap"/>), which is also what
/// makes the repair one undo step.
/// </para>
/// <para>
/// The action is addressed by node id rather than by the caret, because a repair names what it repairs. It
/// works on a node in a list slot as readily as on the only child of a single-valued one, and it declines
/// with a reason rather than throwing when the document moved on between the check that built the repair
/// and the gesture that applied it.
/// </para>
/// </remarks>
public sealed class WrapInAction : EditAction
{
    private readonly NodeId _node;
    private readonly string _wrapperConcept;
    private readonly string _wrapperSlot;

    /// <summary>Surrounds <paramref name="node"/> with a new node of <paramref name="wrapperConceptKey"/>.</summary>
    /// <param name="node">The node to surround, which keeps its identity and stays in its own place.</param>
    /// <param name="wrapperConceptKey">The concept of the node to create around it.</param>
    /// <param name="wrapperSlot">
    /// The single-child slot of that concept which receives the node - <c>inner</c> for
    /// <c>Expressions.Parenthesized</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="node"/> is empty, or <paramref name="wrapperConceptKey"/> or
    /// <paramref name="wrapperSlot"/> is not.
    /// </exception>
    public WrapInAction(NodeId node, string wrapperConceptKey, string wrapperSlot)
    {
        if (node.IsEmpty)
        {
            throw new ArgumentException("A wrap needs a node.", nameof(node));
        }

        ArgumentException.ThrowIfNullOrEmpty(wrapperConceptKey);
        ArgumentException.ThrowIfNullOrEmpty(wrapperSlot);

        _node = node;
        _wrapperConcept = wrapperConceptKey;
        _wrapperSlot = wrapperSlot;
    }

    /// <summary>The node that is surrounded.</summary>
    public NodeId Node => _node;

    /// <summary>The concept of the wrapper that is created.</summary>
    public string WrapperConcept => _wrapperConcept;

    /// <summary>The single-child slot of the wrapper that receives the node.</summary>
    public string WrapperSlot => _wrapperSlot;

    /// <inheritdoc/>
    /// <remarks>
    /// The caret stays where it was: what it was standing in is still there, one level deeper, and its
    /// address did not change because the node kept its id. Moving the caret would hide a repair whose
    /// result is visible exactly where the user was looking.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.Document.Model.Project.FindNode(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this repair surrounds is no longer in the document.");
        }

        if (node.Parent is not { } parent || node.ParentSlot is not { } slot)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' is not inside anything, so there is nowhere to put a wrapper.");
        }

        if (!context.Concepts.TryGet(_wrapperConcept, out var wrapper))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_wrapperConcept}' is not part of the language of this document.");
        }

        if (parent.Concept.FindChild(slot) is not { } place || !wrapper.IsKindOf(place.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_wrapperConcept}' cannot stand where '{node.Concept.Key}' stands.");
        }

        if (wrapper.FindChild(_wrapperSlot) is not { Cardinality: Cardinality.One } inner
            || !node.Concept.IsKindOf(inner.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_wrapperConcept}' has no slot that could hold a '{node.Concept.Key}'.");
        }

        context.WrapChild(node, _wrapperConcept, _wrapperSlot);

        return EditResult.Applied(selection);
    }

    public override string ToString() => $"wrap {_node} in {_wrapperConcept}";
}
