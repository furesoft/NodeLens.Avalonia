using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// What an <see cref="EditAction"/> may look at and do while it runs.
/// </summary>
/// <remarks>
/// <para>
/// The context is a view of one apply, not a bag of services: it holds the tree and the layout the
/// caret was resolved against, so an action that asks where a cell is cannot get an answer from a
/// different generation of the tree than the one the caret belongs to.
/// </para>
/// <para>
/// It also hands out the only three mutations the editing layer is allowed to perform - resolve a node,
/// set a property, add a child - and each of them goes through the model's own validated mutators. That
/// is what keeps invariant "the model is only changed through a transaction" and the change journal
/// true without the editing layer enforcing them, and it is why undo works for free: the journal sees
/// every edit because the model produced it.
/// </para>
/// </remarks>
public sealed class EditContext
{
    private readonly Node _document;
    private readonly IReadOnlyList<CaretPlace> _places;

    internal EditContext(
        Node document,
        EditorTree tree,
        CellLayout layout,
        ITextMeasure measure,
        Selection selection)
    {
        _document = document;
        _places = CaretPlaces.Of(tree);

        Tree = tree;
        Layout = layout;
        Measure = measure;
        Selection = selection;
    }

    /// <summary>The node being edited, which is the root of the tree.</summary>
    public Node Document => _document;

    /// <summary>The tree the caret was resolved against.</summary>
    public EditorTree Tree { get; }

    /// <summary>The layout of <see cref="Tree"/>, for the moves that need the page.</summary>
    public CellLayout Layout { get; }

    /// <summary>The measure the layout was computed with.</summary>
    public ITextMeasure Measure { get; }

    /// <summary>The selection as it was when the action started.</summary>
    public Selection Selection { get; }

    /// <summary>The concepts of the language the document is written in.</summary>
    public ConceptRegistry Concepts => _document.Model.Concepts;

    /// <summary>Every cell the caret can stand in, in document order.</summary>
    public IReadOnlyList<CaretPlace> Places => _places;

    /// <summary>The caret stop <paramref name="position"/> means, or <see langword="null"/> when it is not one.</summary>
    public CaretPlace? PlaceAt(CaretPosition position) => CaretPlaces.PlaceAt(_places, position);

    /// <summary>The node that owns the cell at <paramref name="address"/>, or <see langword="null"/>.</summary>
    /// <remarks>
    /// The owner is found by walking the document rather than by keeping a lookup table, because the
    /// tree is rebuilt after every change and a table would be one more thing that can be stale. The
    /// walk is over the same order the builder used, so the answer is the node whose slot the address
    /// names - the innermost one, since an address carries the node id it belongs to.
    /// </remarks>
    public Node? NodeOf(CellAddress address)
    {
        if (address.Owner.IsEmpty)
        {
            return null;
        }

        return Find(_document, address.Owner);
    }

    /// <summary>Sets a property from the text of its cell, if the text is valid for its type.</summary>
    /// <returns>Whether the model was changed.</returns>
    /// <remarks>
    /// <para>
    /// The validity check is not defensive: <see cref="Node.Set(string, string)"/> throws for a value
    /// the type rejects, and refusing here turns "the user typed a letter into a number" from an
    /// exception into a rejected edit.
    /// </para>
    /// <para>
    /// A default is a materialized value, not an absence, so typing into a property that still holds its
    /// default inserts into that text the way it would into a value the user had typed. Replacing the
    /// default needs a way to tell an unset property from a set one, which the model does not have yet;
    /// until it does, this is a limitation rather than the behaviour a user expects.
    /// </remarks>
    public bool SetProperty(PropertyCell cell, string text)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(text);

        if (NodeOf(cell.Address) is not { } node || !cell.Type.IsValid(text))
        {
            return false;
        }

        node.Set(cell.Slot, text);

        return true;
    }

    /// <summary>Adds a new child of <paramref name="conceptKey"/> to <paramref name="parent"/>.</summary>
    /// <exception cref="EditingException"><paramref name="parent"/> is not in the session's model.</exception>
    public Node AddChild(Node parent, string slot, string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        if (!ReferenceEquals(parent.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {parent.Id} belongs to another model than the document being edited.");
        }

        return parent.Model.NewChild(parent, slot, conceptKey);
    }

    private static Node? Find(Node node, NodeId id)
    {
        if (node.Id.Equals(id))
        {
            return node;
        }

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            if (Find(child, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
