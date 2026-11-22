using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Takes a node out of where it stands into a declared place, and leaves a use of it behind.</summary>
/// <remarks>
/// <para>
/// The facts a language owns, not the framework: which ancestor receives the node (<c>intoConcept</c> and the
/// slot on it that holds it), what stands in the node's place afterwards (<c>useConcept</c>), and which of the
/// use's references points at the definition (<c>useReference</c>). A fourth is optional - the property that
/// receives the definition's name - and it exists because a node left blank in a slot whose concept declares a
/// name slot reads as a hole, and an extract cannot ask for a name.
/// </para>
/// <para>
/// It is one action rather than a program of move, create and point, because a program is a list of built
/// actions applied against a tree rebuilt between them: nothing carries the identity of a node one step creates
/// into the next, and the reference has to name exactly that node. Solving it with a placeholder id would make
/// every action honour a second addressing rule.
/// </para>
/// <para>
/// The order is the model's, not an accident: the node leaves first, because a single-valued slot refuses a
/// second child and the use takes the node's own place - the same order <c>Intent.Inline</c> uses in reverse.
/// The node keeps its identity through the move, so every reference into its subtree and every caret address
/// that names one of its cells still lands, and the caret follows it to the place it was extracted into. The
/// three mutations sit inside one model transaction, because a session wraps only a <em>program</em> in one: an
/// action is expected to be one undo step, which is why <c>EditContext.AddChild</c> with a property opens a
/// transaction too.
/// </para>
/// <para>
/// Everything a destination can refuse is answered as a reason before the first mutation, because a refusal
/// does not roll the model back: an action that had already moved the node could not undo it. A declaration
/// computed long ago, and a caret in a slot that has changed since, are ordinary cases rather than programming
/// errors.
/// </para>
/// </remarks>
public sealed class ExtractNodeAction : EditAction
{
    private readonly NodeId _node;
    private readonly string _intoConcept;
    private readonly string _intoSlot;
    private readonly string _useConcept;
    private readonly string _useReference;
    private readonly string? _useName;

    /// <summary>Extracts <paramref name="node"/> into the place <paramref name="intoConcept"/> declares.</summary>
    /// <param name="node">The node to extract; it has to be part of the document being edited.</param>
    /// <param name="intoConcept">The concept of the ancestor that receives it.</param>
    /// <param name="intoSlot">The child slot of that ancestor which holds it.</param>
    /// <param name="useConcept">The concept of the node left in the extracted node's place.</param>
    /// <param name="useReference">The reference slot of <paramref name="useConcept"/> that points at it.</param>
    /// <param name="useName">
    /// The property of <paramref name="useConcept"/> that receives the extracted node's name, or
    /// <see langword="null"/> to write none.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="node"/> is empty, or one of the names is empty when it was passed at all.
    /// </exception>
    public ExtractNodeAction(
        NodeId node,
        string intoConcept,
        string intoSlot,
        string useConcept,
        string useReference,
        string? useName = null)
    {
        if (node.IsEmpty)
        {
            throw new ArgumentException("An extract needs a node.", nameof(node));
        }

        ArgumentException.ThrowIfNullOrEmpty(intoConcept);
        ArgumentException.ThrowIfNullOrEmpty(intoSlot);
        ArgumentException.ThrowIfNullOrEmpty(useConcept);
        ArgumentException.ThrowIfNullOrEmpty(useReference);

        if (useName is not null)
        {
            ArgumentException.ThrowIfNullOrEmpty(useName, nameof(useName));
        }

        _node = node;
        _intoConcept = intoConcept;
        _intoSlot = intoSlot;
        _useConcept = useConcept;
        _useReference = useReference;
        _useName = useName;
    }

    /// <summary>The node that is extracted.</summary>
    public NodeId Node => _node;

    /// <summary>The concept of the ancestor that receives it.</summary>
    public string IntoConcept => _intoConcept;

    /// <summary>The child slot of that ancestor it lands in.</summary>
    public string IntoSlot => _intoSlot;

    /// <summary>The concept of the node left in its place.</summary>
    public string UseConcept => _useConcept;

    /// <summary>The reference slot of the use that points at the extracted node.</summary>
    public string UseReference => _useReference;

    /// <summary>The property of the use that receives the extracted node's name, or <see langword="null"/>.</summary>
    public string? UseName => _useName;

    /// <inheritdoc/>
    /// <remarks>
    /// The caret is answered unchanged, which means it travels with the node: the cell it stood in still exists
    /// because the node kept its identity, and the place it now stands is the answer the gesture asked for.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (context.NodeOf(_node) is not { } node)
        {
            return EditResult.Rejected(selection, "The node this entry extracts is no longer in the document.");
        }

        if (node.Parent is not { } parent || node.ParentSlot is not { } place)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' is not inside anything, so there is nothing to extract it out of.");
        }

        if (AncestorOf(node, _intoConcept) is not { } into)
        {
            return EditResult.Rejected(
                selection,
                $"There is no '{_intoConcept}' above this node, so there is nowhere to extract it to.");
        }

        if (into.Concept.FindChild(_intoSlot) is not { } destination)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{into.Concept.Key}' declares no child slot '{_intoSlot}', so nothing can be extracted there.");
        }

        if (!node.Concept.IsKindOf(destination.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' cannot stand in slot '{_intoSlot}', which holds '{destination.ChildConceptKey}'.");
        }

        if (ReferenceEquals(node.Parent, into) && string.Equals(node.ParentSlot, _intoSlot, StringComparison.Ordinal))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{node.Concept.Key}' already stands in '{_intoSlot}' of '{into.Concept.Key}', so there is nothing to extract.");
        }

        if (destination.Cardinality == Cardinality.One && into.GetChildren(_intoSlot).Count > 0)
        {
            return EditResult.Rejected(
                selection,
                $"Slot '{_intoSlot}' of '{into.Concept.Key}' holds at most one child, and it is filled.");
        }

        if (!context.Concepts.TryGet(_useConcept, out var use))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_useConcept}' is not part of the language of this document.");
        }

        if (parent.Concept.FindChild(place) is not { } standing || !use.IsKindOf(standing.ChildConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_useConcept}' cannot stand where '{node.Concept.Key}' stands.");
        }

        if (use.FindReference(_useReference) is not { } reference)
        {
            return EditResult.Rejected(
                selection,
                $"Concept '{_useConcept}' declares no reference named '{_useReference}', so the use could not point at its own definition.");
        }

        if (!node.Concept.IsKindOf(reference.TargetConceptKey))
        {
            return EditResult.Rejected(
                selection,
                $"Reference '{_useReference}' of '{_useConcept}' cannot point at a '{node.Concept.Key}'.");
        }

        if (_useName is { } nameSlot)
        {
            if (use.FindProperty(nameSlot) is not { } nameProperty)
            {
                return EditResult.Rejected(
                    selection,
                    $"Concept '{_useConcept}' declares no property named '{nameSlot}', so the name has nowhere to go.");
            }

            // The name is text, so a property that cannot hold the name it is about to be given would leave the
            // extract half done - refused here, before anything moves.
            if (node.Name is { Length: > 0 } name && !nameProperty.Type.IsValid(name))
            {
                return EditResult.Rejected(
                    selection,
                    $"'{name}' is not a valid value for '{_useConcept}.{nameSlot}'.");
            }
        }

        var at = node.IndexInParent;
        var end = into.GetChildren(_intoSlot).Count;

        // Three mutations, one undo step: the same reason `EditContext.AddChild` with a property opens a
        // transaction of its own. Without it the move, the node and the reference would be three steps the user has
        // to press undo for, and a session only wraps a *program* in one - an action is expected to be one edit.
        using var transaction = node.Model.BeginTransaction();

        context.MoveChild(into, _intoSlot, node, end);

        var created = context.AddChild(parent, place, at, _useConcept);

        if (_useName is { } slot && node.Name is { Length: > 0 } written)
        {
            context.SetProperty(created, slot, written);
        }

        context.SetReference(created, _useReference, node);

        transaction.Complete();

        return EditResult.Applied(selection);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"extract {_node} into {_intoConcept}.{_intoSlot} as {_useConcept}.{_useReference}";

    /// <summary>The nearest node above <paramref name="node"/> whose concept is <paramref name="conceptKey"/>.</summary>
    private static Node? AncestorOf(Node node, string conceptKey)
    {
        for (var above = node.Parent; above is not null; above = above.Parent)
        {
            if (above.Concept.IsKindOf(conceptKey))
            {
                return above;
            }
        }

        return null;
    }
}
