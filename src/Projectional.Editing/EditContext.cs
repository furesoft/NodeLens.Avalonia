using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;

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
        IEditorClipboard clipboard,
        ITypingRules typing,
        IScopeProvider scopes)
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
        Scopes = scopes;
    }

    /// <summary>The node being edited, which is the root of the tree.</summary>
    public Node Document => _document;

    /// <summary>
    /// What a reference may point at, which is the aspect a language replaces when the default chain is
    /// not what it means.
    /// </summary>
    public IScopeProvider Scopes { get; }

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

    /// <summary>
    /// The characters typed at this edge that did nothing yet, in the order they arrived.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A keyword is spelled one character at a time, and no single one of them means anything: <c>and</c> only
    /// becomes a conjunction when the third letter arrives. So the keystrokes that changed nothing are kept, and
    /// the next one is asked about together with them - which is what lets a language declare a gesture for a
    /// word rather than for a letter.
    /// </para>
    /// <para>
    /// Only text the model refused is kept. A character a property accepted, or one a typing rule turned into a
    /// node, reset it, because the user is writing a value rather than spelling a gesture.
    /// </para>
    /// <para>
    /// View state, like <see cref="Completion"/>: the session seeds it before running a typing action and reads it
    /// back afterwards, and any other action clears it.
    /// </para>
    /// </remarks>
    public string? PendingText { get; internal set; }

    /// <summary>The clipboard Copy, Cut and Paste go through.</summary>
    /// <remarks>
    /// Never <see langword="null"/>: the session supplies one, falling back to an
    /// <see cref="InMemoryClipboard"/> when the host named none, so an action can copy without asking
    /// whether a clipboard exists. Which clipboard it is - the platform's or the session's own - is a
    /// wiring decision the editing layer never makes.
    /// </remarks>
    public IEditorClipboard Clipboard { get; }

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
    public Node? NodeOf(CellAddress address) => address.Owner.IsEmpty ? null : NodeOf(address.Owner);

    /// <summary>The node with <paramref name="id"/>, or <see langword="null"/> when it is no longer in the model.</summary>
    public Node? NodeOf(NodeId id) => id.IsEmpty ? null : Find(_document, id);

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

    /// <summary>
    /// Sets a property of <paramref name="node"/> by name, whether or not the notation draws it.
    /// </summary>
    /// <returns>Whether the model was changed.</returns>
    /// <remarks>
    /// <para>
    /// The same validation as the cell-shaped overload and for the same reason, without requiring a cell to exist
    /// first. A property can be invisible for three reasons - the notation puts it behind a
    /// <see cref="ConditionalNotationCell"/>, the node is drawn collapsed, or the concept's notation leaves the
    /// slot out - and none of them is a reason the language cannot state the value.
    /// </para>
    /// <para>
    /// The caller names the node it means, so this is not a way to reach around the caret: an edit that touches a
    /// node the user is not looking at says so by passing that node's id.
    /// </para>
    /// </remarks>
    public bool SetProperty(Node node, string slot, string text)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(text);

        if (!ReferenceEquals(node.Model, _document.Model)
            || node.Concept.FindProperty(slot) is not { } property
            || !property.Type.IsValid(text))
        {
            return false;
        }

        node.Set(slot, text);

        return true;
    }

    /// <summary>
    /// Points the reference in <paramref name="slot"/> of <paramref name="node"/> at <paramref name="target"/>.
    /// </summary>
    /// <param name="node">The node that owns the reference.</param>
    /// <param name="slot">The reference slot.</param>
    /// <param name="target">The node to point at.</param>
    /// <returns>Whether the reference was set.</returns>
    /// <remarks>
    /// <para>
    /// The owner has to be part of the document being edited, because the edit is an edit of *this* document.
    /// The target does not: a reference may cross a model boundary but not a project one, which is the model's own
    /// rule (<see cref="Node.SetReference(string, Node?)"/>) - and the scope provider offers a project's other
    /// documents by name on purpose, so refusing one here would have made the list offer something the edit
    /// rejects. This is the same rule the SQL schema sample found, kept in one place rather than two.
    /// </para>
    /// <para>
    /// The target's name travels with the link as its <c>resolveInfo</c>, so a reference whose target is
    /// later removed still says what it meant - the same fallback a loaded document uses.
    /// </para>
    /// </remarks>
    public bool SetReference(Node node, string slot, Node target)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(target);

        if (!ReferenceEquals(node.Model, _document.Model)
            || !ReferenceEquals(target.Model.Project, _document.Model.Project)
            || node.Concept.FindReference(slot) is not { } reference
            || !target.Concept.IsKindOf(reference.TargetConceptKey))
        {
            return false;
        }

        node.SetReference(slot, target.Id, target.Name);

        return true;
    }

    /// <summary>Clears the reference in <paramref name="slot"/> of <paramref name="node"/>.</summary>
    /// <param name="node">The node that owns the reference.</param>
    /// <param name="slot">The reference slot, which must belong to the node's concept.</param>
    /// <returns>Whether the reference was cleared.</returns>
    /// <remarks>
    /// A reference nobody has set and one that was cleared are the same state, so this answers
    /// <see langword="true"/> for both: the caller asked for "no target here", and that is what the node holds.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public bool ClearReference(Node node, string slot)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (!ReferenceEquals(node.Model, _document.Model) || node.Concept.FindReference(slot) is null)
        {
            return false;
        }

        node.SetReference(slot, target: null);

        return true;
    }

    /// <summary>Puts the only child of <paramref name="slot"/> where <paramref name="node"/> stands.</summary>
    /// <param name="node">The node that gives up its place; it must be part of the document being edited.</param>
    /// <param name="slot">A child slot of its concept that holds exactly one child.</param>
    /// <returns>Whether the unwrap ran.</returns>
    /// <remarks>
    /// The checks here are the ones this layer owns - the node has to be part of the document, the slot has to
    /// exist and hold exactly one child. What the model adds on top (conformance, applicability, "the node holds
    /// nothing else") is deliberately not repeated: the model is the authority on a legal placement, and a second
    /// copy of those rules here would be a copy that can drift.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ModelInvariantException">The model refuses the placement.</exception>
    public bool UnwrapChild(Node node, string slot)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (!ReferenceEquals(node.Model, _document.Model)
            || node.Parent is null
            || node.Concept.FindChild(slot) is null
            || node.GetChildren(slot).Count != 1)
        {
            return false;
        }

        node.Model.Unwrap(node, slot);

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
    /// Adds a new child of <paramref name="conceptKey"/> to <paramref name="parent"/> at
    /// <paramref name="index"/>, which is where the caret was.
    /// </summary>
    /// <param name="parent">The node that gains the child.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/>.</param>
    /// <param name="index">Where among the slot's children the new node goes.</param>
    /// <param name="conceptKey">The concept of the new child.</param>
    /// <returns>The node that was created.</returns>
    /// <exception cref="EditingException"><paramref name="parent"/> is not in the session's model.</exception>
    /// <remarks>
    /// Separate from <see cref="AddChild(Node, string, string)"/> rather than an optional parameter, so that a
    /// caller which does not know a position cannot silently get "append" - the one place that asks for a
    /// position is the key that knows where the caret stands.
    /// </remarks>
    public Node AddChild(Node parent, string slot, int index, string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        if (!ReferenceEquals(parent.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {parent.Id} belongs to another model than the document being edited.");
        }

        return parent.Model.NewChildAt(parent, slot, index, conceptKey);
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

        return WrapChildren([node], wrapperConceptKey, wrapperSlot, presets);
    }

    /// <summary>Puts a run of sibling nodes inside one new node, keeping their identity and their order.</summary>
    /// <param name="nodes">The nodes to surround, which must be consecutive siblings.</param>
    /// <param name="wrapperConceptKey">The concept of the node to create around them.</param>
    /// <param name="wrapperSlot">The slot of that concept which receives them.</param>
    /// <param name="presets">Values the wrapper is born with, or <see langword="null"/>.</param>
    /// <returns>The node that was created.</returns>
    /// <remarks>
    /// The run form exists for the same reason the one-node form does: a selection of several siblings is
    /// surrounded by one gesture, so it has to be one edit. Everything about the place - whether the nodes
    /// are consecutive, whether the wrapper may stand there, whether the slot can hold them all - is asked
    /// by the model, which is where the invariant lives and where an undo closure can be written for it.
    /// </remarks>
    public Node WrapChildren(
        IReadOnlyList<Node> nodes,
        string wrapperConceptKey,
        string wrapperSlot,
        IReadOnlyDictionary<string, string>? presets = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentException.ThrowIfNullOrEmpty(wrapperConceptKey);
        ArgumentException.ThrowIfNullOrEmpty(wrapperSlot);

        foreach (var node in nodes)
        {
            if (!ReferenceEquals(node.Model, _document.Model))
            {
                throw new EditingException(
                    $"Node {node.Id} belongs to another model than the document being edited.");
            }
        }

        return _document.Model.Wrap(nodes, wrapperConceptKey, wrapperSlot, presets);
    }

    /// <summary>
    /// Creates the nodes of <paramref name="fragment"/> in <paramref name="slot"/> of
    /// <paramref name="parent"/>, starting at <paramref name="index"/>.
    /// </summary>
    /// <param name="fragment">The copied subtrees to insert, in the order they should appear.</param>
    /// <param name="parent">The node that gains them.</param>
    /// <param name="slot">The child slot of <paramref name="parent"/> which receives them.</param>
    /// <param name="index">Where among the slot's children the first root goes.</param>
    /// <returns>The node created for the fragment's first root.</returns>
    /// <remarks>
    /// <para>
    /// Everything happens in one transaction, so a fragment is one undo step and a failure cannot leave half of
    /// it behind. The nodes are created top-down - attached first, then filled - because that is the order the
    /// mutation API offers and the order that keeps every node reachable while it is being written.
    /// </para>
    /// <para>
    /// References are written in a second pass, because a fragment may point forward: a rule can refer to a
    /// sibling that has not been created yet. A reference between two copied nodes is rebound to the copy; one
    /// that pointed outside the fragment keeps its target id and its <c>resolveInfo</c>, so it either resolves
    /// in the target project or draws as unresolved and can be re-linked - the same degradation a loaded
    /// document gets (ADR 0004). Nothing is silently dropped.
    /// </para>
    /// <para>
    /// The fragment must be expressible in this document's language; a property value the target concept
    /// refuses is an exception rather than a skipped value, because dropping it would be a paste that loses
    /// what it copied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fragment"/> or <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="slot"/> is empty, or the fragment holds nothing.
    /// </exception>
    /// <exception cref="EditingException">
    /// <paramref name="parent"/> belongs to another model, the language does not declare a concept the fragment
    /// names, or a value could not be written.
    /// </exception>
    public Node InsertFragment(ModelFragment fragment, Node parent, string slot, int index)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (fragment.IsEmpty)
        {
            throw new ArgumentException("A paste needs a fragment with at least one node.", nameof(fragment));
        }

        if (!ReferenceEquals(parent.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {parent.Id} belongs to another model than the document being edited.");
        }

        if (fragment.MissingConcept(_document.Model.Concepts) is { } missing)
        {
            throw new EditingException(
                $"Concept '{missing}' is not part of the language of this document, so the fragment cannot be pasted here.");
        }

        List<(Node Node, FragmentNode Piece)> created = [];
        Dictionary<NodeId, NodeId> rebinding = [];
        var model = _document.Model;

        using var transaction = model.BeginTransaction();

        var at = index;

        foreach (var root in fragment.Roots)
        {
            Create(root, parent, slot, at, created, rebinding);
            at++;
        }

        foreach (var (node, piece) in created)
        {
            foreach (var (name, value) in piece.Properties)
            {
                if (!SetProperty(node, name, value))
                {
                    throw new EditingException(
                        $"Value \"{value}\" is not valid for property '{name}' of concept '{piece.ConceptKey}'.");
                }
            }

            foreach (var reference in piece.References)
            {
                var target = rebinding.TryGetValue(reference.Target, out var copy) ? copy : reference.Target;

                node.SetReference(reference.Slot, target, reference.ResolveInfo);
            }
        }

        transaction.Complete();

        return created[0].Node;
    }

    /// <summary>Creates one copied node and everything below it, remembering where each copy came from.</summary>
    /// <remarks>
    /// Recursive rather than iterative because a child is created into its new parent, so the parent has to exist
    /// first - the recursion <em>is</em> the attach order.
    /// </remarks>
    private void Create(
        FragmentNode piece,
        Node parent,
        string slot,
        int index,
        List<(Node Node, FragmentNode Piece)> created,
        Dictionary<NodeId, NodeId> rebinding)
    {
        var node = AddChild(parent, slot, index, piece.ConceptKey);

        rebinding[piece.Origin] = node.Id;
        created.Add((node, piece));

        foreach (var child in piece.Children)
        {
            Create(child.Node, node, child.Slot, node.GetChildren(child.Slot).Count, created, rebinding);
        }
    }

    /// <summary>
    /// Puts a new node of <paramref name="conceptKey"/> where <paramref name="node"/> stands, deleting that
    /// node and everything inside it.
    /// </summary>
    /// <param name="node">The node whose place is taken.</param>
    /// <param name="conceptKey">The concept of the node that takes its place.</param>
    /// <returns>The node that was created, already attached where <paramref name="node"/> was.</returns>
    /// <exception cref="EditingException"><paramref name="node"/> is not in the session's model.</exception>
    /// <remarks>
    /// The model's own operation, for the same reason a wrap is: the old node has to leave before the new one
    /// arrives in a single-valued slot, and one place has to stay one undo step. The concept is not validated
    /// here - the model refuses a concept that cannot stand there, before it deletes anything.
    /// </remarks>
    public Node ReplaceChild(Node node, string conceptKey)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);

        if (!ReferenceEquals(node.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {node.Id} belongs to another model than the document being edited.");
        }

        return node.Model.Replace(node, conceptKey);
    }

    /// <summary>Puts the children of <paramref name="slot"/> into <paramref name="order"/>.</summary>
    /// <param name="parent">The node whose children are reordered.</param>
    /// <param name="slot">A child slot declared with <see cref="Cardinality.Many"/>.</param>
    /// <param name="order">Every child of that slot, exactly once, in the order they should have afterwards.</param>
    /// <exception cref="EditingException"><paramref name="parent"/> is not in the session's model.</exception>
    /// <remarks>
    /// The order is the caller's decision and the mutation is the model's, which is the division that keeps "sorted by
    /// name" out of the model: a second caller with a second rule needs no second primitive.
    /// </remarks>
    public void ReorderChildren(Node parent, string slot, IReadOnlyList<Node> order)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(order);

        if (!ReferenceEquals(parent.Model, _document.Model))
        {
            throw new EditingException(
                $"Node {parent.Id} belongs to another model than the document being edited.");
        }

        parent.Model.ReorderChildren(parent, slot, order);
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
