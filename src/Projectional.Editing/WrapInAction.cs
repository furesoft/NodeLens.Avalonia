using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Puts one node, or a run of sibling nodes, inside a newly created wrapper, keeping their identity and
/// their subtrees.
/// </summary>
/// <remarks>
/// <para>
/// This is the one genuinely new structural edit the quick-fix catalogue needed: "wrap in parentheses" is
/// the repair for an expression whose grouping a reader has to infer, and no combination of the existing
/// actions can express it. A single-valued slot holds at most one child, so creating the wrapper and then
/// moving the node into it would need a moment in which both are in the slot; the model performs the
/// detach-create-attach sequence as one transaction instead (<see cref="Model.Wrap(IReadOnlyList{Node}, string, string, IReadOnlyDictionary{string, string})"/>),
/// which is also what makes the repair one undo step.
/// </para>
/// <para>
/// The run form is the same operation with a selection instead of a caret: several siblings take the place
/// of one node, the wrapper receives them in the order a reader sees them, and the whole change is still
/// one undo step. "Wrap these three steps in a loop" is what a user selecting them means, and a menu entry
/// that wrapped only the one under the caret would be a different edit under the same label.
/// </para>
/// <para>
/// The action is addressed by node id rather than by the caret, because a repair names what it repairs. It
/// works on nodes in a list slot as readily as on the only child of a single-valued one, and it declines
/// with a reason rather than throwing when the document moved on between the check that built the entry
/// and the gesture that applied it - including when the nodes stopped being consecutive siblings.
/// </para>
/// </remarks>
public sealed class WrapInAction : EditAction
{
    private readonly IReadOnlyList<NodeId> _nodes;
    private readonly string _wrapperConcept;
    private readonly string _wrapperSlot;
    private readonly IReadOnlyDictionary<string, string>? _presets;

    /// <summary>Surrounds <paramref name="node"/> with a new node of <paramref name="wrapperConceptKey"/>.</summary>
    /// <param name="node">The node to surround, which keeps its identity and stays in its own place.</param>
    /// <param name="wrapperConceptKey">The concept of the node to create around it.</param>
    /// <param name="wrapperSlot">
    /// The single-child slot of that concept which receives the node - <c>inner</c> for
    /// <c>Expressions.Parenthesized</c>.
    /// </param>
    /// <param name="presets">
    /// Values the wrapper is born with - <c>operator = "!"</c> for a negation - or <see langword="null"/>
    /// when its defaults are the whole meaning.
    /// </param>
    /// <remarks>
    /// A preset is not a second edit: the wrapper does not exist until the wrap runs, so its id cannot be
    /// addressed beforehand. The values therefore travel with the wrap and are validated against the
    /// wrapper's declarations before anything moves, which is what keeps "wrap in a negation" one undo step.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="node"/> is empty, or <paramref name="wrapperConceptKey"/> or
    /// <paramref name="wrapperSlot"/> is not.
    /// </exception>
    public WrapInAction(
        NodeId node,
        string wrapperConceptKey,
        string wrapperSlot,
        IReadOnlyDictionary<string, string>? presets = null)
        : this([node], wrapperConceptKey, wrapperSlot, presets)
    {
    }

    /// <summary>Surrounds a run of sibling nodes with one new node of <paramref name="wrapperConceptKey"/>.</summary>
    /// <param name="nodes">The nodes to surround, in document order, which must be consecutive siblings.</param>
    /// <param name="wrapperConceptKey">The concept of the node to create around them.</param>
    /// <param name="wrapperSlot">The slot of that concept which receives them.</param>
    /// <param name="presets">Values the wrapper is born with, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="nodes"/> is empty or contains an empty id, or <paramref name="wrapperConceptKey"/> or
    /// <paramref name="wrapperSlot"/> is empty.
    /// </exception>
    public WrapInAction(
        IReadOnlyList<NodeId> nodes,
        string wrapperConceptKey,
        string wrapperSlot,
        IReadOnlyDictionary<string, string>? presets = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentException.ThrowIfNullOrEmpty(wrapperConceptKey);
        ArgumentException.ThrowIfNullOrEmpty(wrapperSlot);

        if (nodes.Count == 0)
        {
            throw new ArgumentException("A wrap needs at least one node.", nameof(nodes));
        }

        foreach (var node in nodes)
        {
            if (node.IsEmpty)
            {
                throw new ArgumentException("A wrap needs nodes, not empty ids.", nameof(nodes));
            }
        }

        _nodes = [.. nodes];
        _wrapperConcept = wrapperConceptKey;
        _wrapperSlot = wrapperSlot;
        _presets = presets;
    }

    /// <summary>The nodes that are surrounded, in document order.</summary>
    public IReadOnlyList<NodeId> Nodes => _nodes;

    /// <summary>The concept of the wrapper that is created.</summary>
    public string WrapperConcept => _wrapperConcept;

    /// <summary>The slot of the wrapper that receives the nodes.</summary>
    public string WrapperSlot => _wrapperSlot;

    /// <summary>The values the wrapper is born with, or <see langword="null"/> when it has none.</summary>
    public IReadOnlyDictionary<string, string>? Presets => _presets;

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
        List<Node> found = [];

        foreach (var id in _nodes)
        {
            if (context.Document.Model.Project.FindNode(id) is not { } node)
            {
                return EditResult.Rejected(selection, "A node this repair surrounds is no longer in the document.");
            }

            found.Add(node);
        }

        var first = found[0];

        if (first.Parent is not { } parent || first.ParentSlot is not { } slot)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{first.Concept.Key}' is not inside anything, so there is nowhere to put a wrapper.");
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
                $"Concept '{_wrapperConcept}' cannot stand where '{first.Concept.Key}' stands.");
        }

        if (wrapper.FindChild(_wrapperSlot) is not { } inner)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_wrapperConcept}' has no slot that could hold a '{first.Concept.Key}'.");
        }

        if (inner.Cardinality == Cardinality.One && found.Count > 1)
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{_wrapperSlot}' of concept '{_wrapperConcept}' holds one child, so it cannot take {found.Count} nodes.");
        }

        var seen = new HashSet<NodeId>();

        foreach (var node in found)
        {
            if (!seen.Add(node.Id))
            {
                return EditResult.Rejected(selection, "The same node cannot be wrapped twice by one edit.");
            }

            if (!ReferenceEquals(node.Parent, parent) || !string.Equals(node.ParentSlot, slot, StringComparison.Ordinal))
            {
                return EditResult.Rejected(
                    selection,
                    $"Concept '{node.Concept.Key}' is not a sibling of '{first.Concept.Key}', so nothing can surround them together.");
            }

            if (!node.Concept.IsKindOf(inner.ChildConceptKey))
            {
                return EditResult.Rejected(
                    selection,
                    $"Concept '{_wrapperConcept}' has no slot that could hold a '{node.Concept.Key}'.");
            }
        }

        found.Sort((left, right) => left.IndexInParent.CompareTo(right.IndexInParent));

        for (var index = 1; index < found.Count; index++)
        {
            if (found[index].IndexInParent != found[index - 1].IndexInParent + 1)
            {
                return EditResult.Rejected(
                    selection,
                    "The nodes are no longer next to each other, so a wrapper would leave one of their siblings outside it.");
            }
        }

        if (_presets is not null)
        {
            foreach (var (presetSlot, value) in _presets)
            {
                if (wrapper.FindProperty(presetSlot) is not { } property)
                {
                    return EditResult.Rejected(
                        selection,
                        $"Concept '{_wrapperConcept}' declares no property named '{presetSlot}'.");
                }

                if (!property.Type.IsValid(value))
                {
                    return EditResult.Rejected(
                        selection,
                        $"\"{value}\" is not a valid {property.Type.Name} value for '{presetSlot}'.");
                }
            }
        }

        context.WrapChildren(found, _wrapperConcept, _wrapperSlot, _presets);

        return EditResult.Applied(selection);
    }

    public override string ToString() =>
        _nodes.Count == 1
            ? $"wrap {_nodes[0]} in {_wrapperConcept}"
            : $"wrap {_nodes.Count} nodes in {_wrapperConcept}";
}
