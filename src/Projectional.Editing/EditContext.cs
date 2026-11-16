using Projectional.Core;
using Projectional.Language;
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
/// It also hands out the only mutations the editing layer is allowed to perform - resolve a node, set a
/// property, add a child, move a child - and each of them goes through the model's own validated
/// mutators. That
/// is what keeps invariant "the model is only changed through a transaction" and the change journal
/// true without the editing layer enforcing them, and it is why undo works for free: the journal sees
/// every edit because the model produced it.
/// </para>
/// </remarks>
public sealed class EditContext
{
    private readonly Node _document;
    private readonly IReadOnlyList<CaretPlace> _places;
    private readonly ITypingRules _typing;

    internal EditContext(
        Node document,
        EditorTree tree,
        CellLayout layout,
        ITextMeasure measure,
        double pageHeight,
        Selection selection,
        ITextClipboard clipboard,
        ITypingRules typing)
    {
        _document = document;
        _places = CaretPlaces.Of(tree);

        Tree = tree;
        Layout = layout;
        Measure = measure;
        PageHeight = pageHeight;
        Selection = selection;
        Clipboard = clipboard;
        _typing = typing;
    }

    /// <summary>The node being edited, which is the root of the tree.</summary>
    public Node Document => _document;

    /// <summary>The tree the caret was resolved against.</summary>
    public EditorTree Tree { get; }

    /// <summary>The layout of <see cref="Tree"/>, for the moves that need the page.</summary>
    public CellLayout Layout { get; }

    /// <summary>The measure the layout was computed with.</summary>
    public ITextMeasure Measure { get; }

    /// <summary>
    /// How much of the layout is visible, in layout units, which is what a page move travels.
    /// </summary>
    /// <remarks>
    /// The layout cannot know this - the same document is drawn in windows of different heights - so it
    /// travels with the action instead. Zero means "no viewport was reported", and a page move then
    /// degrades into the single-line move it is a version of.
    /// </remarks>
    public double PageHeight { get; }

    /// <summary>The selection as it was when the action started.</summary>
    public Selection Selection { get; }

    /// <summary>
    /// The completion list that was open when the action started, and the one that will be open when it
    /// ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// View state, like the notation variant: it is never persisted and never produces an undo step of
    /// its own. An action reads it to decide what a keystroke means while a list is open, and writes it
    /// to narrow, move, accept or dismiss.
    /// </para>
    /// <para>
    /// The session seeds this before running any action and reads it back afterwards, which is what makes
    /// "anything but a completion gesture closes the list" one rule in <see cref="EditorSession"/> rather
    /// than a rule every action has to remember.
    /// </para>
    /// </remarks>
    public CompletionList? Completion { get; internal set; }

    /// <summary>The clipboard Copy, Cut and Paste go through.</summary>
    /// <remarks>
    /// Never <see langword="null"/>: the session supplies one, falling back to an
    /// <see cref="InMemoryClipboard"/> when the host named none, so an action can copy without asking
    /// whether a clipboard exists. Which clipboard it is - the platform's or the session's own - is a
    /// wiring decision the editing layer never makes.
    /// </remarks>
    public ITextClipboard Clipboard { get; }

    /// <summary>The concepts of the language the document is written in.</summary>
    public ConceptRegistry Concepts => _document.Model.Concepts;

    /// <summary>
    /// What the language says about text that arrives while the caret is in an empty slot.
    /// </summary>
    /// <remarks>
    /// Never <see langword="null"/>: a session given no rules gets <see cref="TypingRuleRegistry.Empty"/>,
    /// so typing into an empty slot is refused the way it was before there were rules, instead of asking
    /// every action whether the language has an opinion.
    /// </remarks>
    public ITypingRules TypingRules => _typing;

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

    /// <summary>
    /// Adds a new child of <paramref name="conceptKey"/> to <paramref name="parent"/> and fills one of
    /// its properties with <paramref name="text"/>, as a single undo step.
    /// </summary>
    /// <param name="parent">The node that gains the child.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/>.</param>
    /// <param name="conceptKey">The concept of the new child.</param>
    /// <param name="propertySlot">
    /// The property of the new child to fill, or <see langword="null"/> when the text only selects the
    /// concept.
    /// </param>
    /// <param name="text">The value to write.</param>
    /// <returns>The node that was created.</returns>
    /// <exception cref="EditingException"><paramref name="parent"/> is not in the session's model.</exception>
    /// <remarks>
    /// <para>
    /// One step rather than two because creating a node and filling it is one gesture to the user: typing
    /// the first character of an empty slot must be undone by one press, not by an undo that leaves an
    /// empty node behind.
    /// </para>
    /// <para>
    /// The text is dropped rather than refused when the concept declares no such property or its type
    /// rejects the value. That is a mistake in the language's typing rule, not something the user did, and
    /// a keystroke must not throw; the visible result - the right concept, unfilled - is the diagnostic.
    /// </para>
    /// </remarks>
    public Node AddChild(Node parent, string slot, string conceptKey, string? propertySlot, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        using var transaction = _document.Model.BeginTransaction();

        var created = AddChild(parent, slot, conceptKey);

        if (propertySlot is not null
            && created.Concept.FindProperty(propertySlot) is { } property
            && property.Type.IsValid(text))
        {
            created.Set(propertySlot, text);
        }

        transaction.Complete();

        return created;
    }

    /// <summary>
    /// Removes <paramref name="child"/> from <paramref name="slot"/> of <paramref name="parent"/> and
    /// deletes its subtree.
    /// </summary>
    /// <exception cref="EditingException"><paramref name="parent"/> is not in the session's model.</exception>
    /// <remarks>
    /// The model's own removal is used rather than a re-parenting of the node, so the subtree leaves the
    /// document as one journalled step: undo puts every node of it back at the place it came from, with
    /// its identity intact.
    /// </remarks>
    public void DeleteChild(Node parent, string slot, Node child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(child);

        if (!ReferenceEquals(parent.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {parent.Id} belongs to another model than the document being edited.");
        }

        parent.RemoveChild(slot, child);
    }

    /// <summary>
    /// Moves <paramref name="child"/> to <paramref name="index"/> among the children of
    /// <paramref name="slot"/> of <paramref name="parent"/>.
    /// </summary>
    /// <exception cref="EditingException"><paramref name="parent"/> is not in the session's model.</exception>
    /// <remarks>
    /// The node is unlinked and put back inside one mutation, so it keeps its identity: its id stays the
    /// same, and with it every reference that points into its subtree and every caret address that names
    /// one of its cells. The journal sees one move rather than a deletion and a creation, which is what
    /// makes undo put the node back where it was instead of rebuilding it somewhere else.
    /// </remarks>
    public Node MoveChild(Node parent, string slot, Node child, int index)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!ReferenceEquals(parent.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {parent.Id} belongs to another model than the document being edited.");
        }

        return parent.InsertChild(slot, index, child);
    }

    /// <summary>
    /// Puts <paramref name="node"/> inside a new node of <paramref name="wrapperConceptKey"/> and
    /// <paramref name="wrapperSlot"/>, keeping its identity.
    /// </summary>
    /// <param name="node">The node to surround.</param>
    /// <param name="wrapperConceptKey">The concept of the node to create around it.</param>
    /// <param name="wrapperSlot">The single-child slot of that concept which receives the node.</param>
    /// <param name="presets">Values the wrapper is born with, or <see langword="null"/> for none.</param>
    /// <exception cref="EditingException"><paramref name="node"/> is not in the session's model.</exception>
    /// <remarks>
    /// A wrap is a structural move, so it goes through the model's own operation rather than being
    /// assembled here out of a create and a move: a single-valued slot cannot hold the node and its wrapper
    /// at the same time, and the model is where that rule lives. The presets travel with it for the same
    /// reason - the wrapper's id does not exist until the wrap runs.
    /// </remarks>
    public Node WrapChild(
        Node node,
        string wrapperConceptKey,
        string wrapperSlot,
        IReadOnlyDictionary<string, string>? presets = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(wrapperConceptKey);
        ArgumentException.ThrowIfNullOrEmpty(wrapperSlot);

        if (!ReferenceEquals(node.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {node.Id} belongs to another model than the document being edited.");
        }

        return node.Model.Wrap(node, wrapperConceptKey, wrapperSlot, presets);
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
