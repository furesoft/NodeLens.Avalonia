using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One open document: the tree, the layout, the caret and the undo history, kept consistent with each
/// other.
/// </summary>
/// <remarks>
/// <para>
/// The session is what an editor control owns per open model. It exists so that the three derived
/// things cannot drift apart: after every applied edit the tree is rebuilt from the model and the
/// layout from the tree, and the caret is resolved against the tree that just appeared. Nothing else
/// in the layer is allowed to hold a tree across a change, which is why the whole cycle is here rather
/// than in the actions.
/// </para>
/// <para>
/// Rebuilding rather than patching is the deliberate choice M1 makes. A change can alter anything - a
/// property may change a cell's text, adding a child may split a line, a substitution may replace the
/// notation - so the honest response to "the model changed" is a new tree. When that becomes too slow,
/// the answer is an invalidation scheme behind this same interface, not incremental mutation of cells.
/// </para>
/// <para>
/// The session also owns the undo boundary policy: it hands the action's coalescing key to the history
/// before applying and restores the previous key afterwards, so the key is only in force for the
/// commits the action itself makes.
/// </para>
/// </remarks>
public sealed class EditorSession : IDisposable
{
    private readonly NotationRegistry _notations;
    private readonly ICellEmphasis? _emphasis;
    private CellLayouter _layouter;
    private double? _wrapWidth;

    private EditorTreeBuilder _builder;

    /// <summary>Opens <paramref name="document"/> for editing.</summary>
    /// <param name="document">The root of the model to edit.</param>
    /// <param name="notations">The notation that projects the document's concepts.</param>
    /// <param name="measure">How text is measured; monospace when omitted, which is what tests use.</param>
    /// <param name="emphasis">
    /// What decorates the model beyond its notation, asked again on every rebuild; nothing when omitted.
    /// </param>
    /// <param name="choice">
    /// Which variant of each concept to draw; the default variant of everything when omitted.
    /// </param>
    /// <param name="clipboard">
    /// Where Copy, Cut and Paste put and take text; the session's own clipboard when omitted, which keeps
    /// copying and pasting working in a host that has no platform clipboard.
    /// </param>
    /// <exception cref="EditingException">The notation produces no cell a caret can stand in.</exception>
    public EditorSession(
        Node document,
        NotationRegistry notations,
        ITextMeasure? measure = null,
        ICellEmphasis? emphasis = null,
        INotationChoice? choice = null,
        ITextClipboard? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(notations);

        Document = document;
        Measure = measure ?? MonospaceTextMeasure.Instance;
        History = new UndoHistory(document.Model);

        _notations = notations;
        _emphasis = emphasis;
        _layouter = new CellLayouter(Measure);
        Choice = choice ?? NotationChoice.Default;
        Clipboard = clipboard ?? new InMemoryClipboard();
        _builder = new EditorTreeBuilder(notations, emphasis, Choice);

        Tree = _builder.Build(Document);
        Layout = _layouter.Layout(Tree);

        // Opening a document puts the caret where the user can start typing, not on the keyword the
        // notation happens to render first.
        var start = CaretPlaces.FirstSlot(Tree);
        Selection = new Selection(start, start);
    }

    /// <summary>The node being edited.</summary>
    public Node Document { get; }

    /// <summary>How text is measured.</summary>
    public ITextMeasure Measure { get; }

    /// <summary>The undo history of the document's model.</summary>
    public UndoHistory History { get; }

    /// <summary>The tree of the current state of the model.</summary>
    public EditorTree Tree { get; private set; }

    /// <summary>The layout of <see cref="Tree"/>.</summary>
    public CellLayout Layout { get; private set; }

    /// <summary>Where the caret is and what is selected.</summary>
    public Selection Selection { get; private set; }

    /// <summary>
    /// Which variant of each concept is drawn, as the tree was built with it. View state, not model
    /// state: two sessions can hold different choices over one document and edit the same model.
    /// </summary>
    public INotationChoice Choice { get; private set; }

    /// <summary>
    /// The completion list open at the caret, or <see langword="null"/> when none is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// View state, like <see cref="Choice"/>: opening a document shows no list, no list is persisted and
    /// no list is an undo step. A host reads this after every <see cref="Apply"/> to draw or hide its
    /// popup, which is why the property is public while the gestures that set it are ordinary actions.
    /// </para>
    /// <para>
    /// It is cleared by anything that is not a completion gesture, by a move of the caret, and by a
    /// rebuild - a list describes the empty slot at a caret, and none of those leave that description
    /// true.
    /// </para>
    /// </remarks>
    public CompletionList? Completion { get; private set; }

    /// <summary>Where Copy, Cut and Paste put and take their text.</summary>
    /// <remarks>
    /// <para>
    /// Never <see langword="null"/>: a session built without one gets an <see cref="InMemoryClipboard"/>,
    /// so the clipboard gestures work in a host with no platform clipboard and in a test with no window.
    /// Handing the same adapter to two sessions is what makes copying between two documents work, which is
    /// a wiring decision this layer leaves to the host.
    /// </para>
    /// <para>
    /// Public because a host needs it for two things: to hand the session a platform adapter, and to decide
    /// whether Paste has anything to offer before it shows a menu entry.
    /// </para>
    /// </remarks>
    public ITextClipboard Clipboard { get; }

    /// <summary>
    /// How wide the room a text cell may fill is, or <see langword="null"/> when it may run on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A text is wrapped at the room left to this edge, measured from the start of its line, so the
    /// width is the host's page and not a column count: a value that starts further right has less room
    /// before it breaks, which is what makes the wrapping follow the indentation instead of ignoring it.
    /// </para>
    /// <para>
    /// View state, like <see cref="Choice"/>: two hosts can show the same document at different widths
    /// and edit the same model. Setting it re-lays-out the document, so a control publishes it from its
    /// measure pass - it is the only way the projection learns how much room there is.
    /// </para>
    /// </remarks>
    public double? WrapWidth
    {
        get => _wrapWidth;

        set
        {
            if (Nullable.Equals(_wrapWidth, value))
            {
                return;
            }

            _wrapWidth = value;
            _layouter = new CellLayouter(Measure, value);
            Refresh();
        }
    }

    /// <summary>
    /// Draws the document with <paramref name="choice"/> from now on.
    /// </summary>
    /// <remarks>
    /// Switching a representation is a re-projection like any other, so it goes through the same
    /// rebuild-and-re-resolve path: the model is untouched, the tree and the layout are new, and the
    /// caret lands on the nearest cell that still exists. A cell the other variant showed and this one
    /// hides is not an error - it is the difference between the two representations.
    /// </remarks>
    public void Choose(INotationChoice? choice)
    {
        Choice = choice ?? NotationChoice.Default;
        _builder = new EditorTreeBuilder(_notations, _emphasis, Choice);
        Refresh();
    }

    /// <summary>
    /// The concepts of this document that declare more than one notation, in the order the document
    /// first uses them.
    /// </summary>
    /// <remarks>
    /// Only concepts the document actually holds are listed, because a representation can only be
    /// switched for a node that is there; the variants of a concept's super concepts are included,
    /// because a variant resolves up the chain. A document whose concepts all declare a single notation
    /// returns nothing, which is how a host knows there is no representation to offer.
    /// </remarks>
    public IReadOnlyList<ConceptVariants> Alternatives()
    {
        var alternatives = new List<ConceptVariants>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in SelfAndDescendants(Document))
        {
            if (!seen.Add(node.Concept.Key))
            {
                continue;
            }

            var variants = VariantsOf(node.Concept);

            if (variants.Count > 1)
            {
                alternatives.Add(new ConceptVariants(
                    node.Concept.Key,
                    variants,
                    Choice.VariantOf(node.Concept.Key) ?? Notation.DefaultVariant));
            }
        }

        return alternatives;
    }

    /// <summary>Draws the nodes of <paramref name="conceptKey"/> in <paramref name="variant"/> from now on.</summary>
    /// <param name="conceptKey">A concept the document holds.</param>
    /// <param name="variant">A variant that applies to it, its own or one it inherits.</param>
    /// <remarks>
    /// The choice is one more opinion on top of the one in force, so a document can be read as a table
    /// in one place and as a block in another. Both arguments are checked against the document, because
    /// a menu that offers a variant the notation does not declare would otherwise re-project to
    /// something that looks like nothing happened.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The document holds no node of <paramref name="conceptKey"/>, or it declares no such variant.
    /// </exception>
    public void ChooseVariant(string conceptKey, string variant)
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ArgumentException.ThrowIfNullOrEmpty(variant);

        var concept = SelfAndDescendants(Document)
            .Select(node => node.Concept)
            .FirstOrDefault(candidate => string.Equals(candidate.Key, conceptKey, StringComparison.Ordinal))
            ?? throw new ArgumentException(
                $"The document holds no node of concept '{conceptKey}', so there is nothing to re-project.",
                nameof(conceptKey));

        if (!VariantsOf(concept).Contains(variant, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Concept '{conceptKey}' declares no '{variant}' notation.",
                nameof(variant));
        }

        Choose((Choice as NotationChoice ?? NotationChoice.Default).With(conceptKey, variant));
    }

    /// <summary>Applies <paramref name="action"/> and leaves the session consistent with the result.</summary>
    /// <remarks>
    /// The selection the action asks for - both ends of it - is resolved against the tree the action
    /// produced, so an action may return an address that only exists after it ran, which is exactly what
    /// adding a child needs. An action that reports no change still gets to move the selection, because
    /// that is how the caret moves at all and how Select All selects anything. An action that created
    /// something to be named gets the value the caret landed in selected; see
    /// <see cref="EditAction.SelectsLandedValue"/>.
    /// </remarks>
    public EditResult Apply(EditAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var context = new EditContext(Document, Tree, Layout, Measure, Selection, Clipboard)
        {
            Completion = action is CompletionAction ? Completion : null,
        };

        var previous = History.CoalescingKey;
        var result = EditResult.Unchanged(Selection);

        try
        {
            History.CoalescingKey = action.CoalescingKey(context);
            result = action.Apply(context);
        }
        finally
        {
            History.CoalescingKey = previous;
        }

        Completion = context.Completion;

        if (result.Changed)
        {
            Rebuild();
        }

        Settle(result.Selection);

        if (action.SelectsLandedValue)
        {
            SelectLandedValue();
        }

        return result;
    }

    /// <summary>Widens the caret to the whole value of the cell it landed in, when that cell has text.</summary>
    /// <remarks>
    /// Read from the tree the edit produced rather than predicted by the action: the coercion has
    /// already decided which cell the caret lands in, so all that is left to say is that the value found
    /// there is the one the user is about to type over. A cell with nothing in it keeps a bare caret,
    /// because selecting an empty range and leaving the caret where it is are the same thing.
    /// </remarks>
    private void SelectLandedValue()
    {
        if (CaretNavigation.PlaceAt(Tree, Selection.Caret) is not { IsEditable: true, Length: > 0 } place)
        {
            return;
        }

        Selection = new Selection(CaretPosition.At(place, 0), CaretPosition.At(place, place.Length));
    }

    /// <summary>Moves the caret, dropping any selection that does not fit the tree.</summary>
    /// <remarks>
    /// Both ends are coerced, so a selection whose anchor was in a deleted node collapses onto the
    /// caret instead of pointing at a cell that no longer exists. Moving the caret also closes any open
    /// completion list, because the proposals describe the slot the caret was in.
    /// </remarks>
    public void Select(Selection selection)
    {
        var anchor = CaretNavigation.Nearest(Tree, selection.Anchor);
        var caret = CaretNavigation.Nearest(Tree, selection.Caret);

        Selection = new Selection(anchor, caret);
        Completion = null;
    }

    /// <summary>Undoes one step of the model's history, keeping the caret on the nearest cell.</summary>
    /// <returns>Whether there was a step to undo.</returns>
    /// <remarks>
    /// The caret is not restored to where it was when the change was made. Undo replays model changes,
    /// which say nothing about carets, so the edited cell may not exist; the nearest cell in the tree
    /// the undo produced is the only answer that is not a guess.
    /// </remarks>
    public bool Undo()
    {
        if (!History.Undo())
        {
            return false;
        }

        Refresh();

        return true;
    }

    /// <summary>Redoes one step of the model's history, keeping the caret on the nearest cell.</summary>
    /// <returns>Whether there was a step to redo.</returns>
    public bool Redo()
    {
        if (!History.Redo())
        {
            return false;
        }

        Refresh();

        return true;
    }

    /// <summary>Rebuilds the tree and the layout from the model, then re-resolves the selection.</summary>
    /// <remarks>
    /// For changes the session did not make - a substitution, a background transformation, a test that
    /// wrote to the model directly. Both ends of the selection are resolved against the tree that just
    /// appeared, so a range stays a range when the cells it named survived and collapses onto the caret
    /// when an end was in a node that is gone; there is nothing better to do with a position that names
    /// a cell that no longer exists. An open completion list is closed for the same reason: it names a
    /// slot of the tree that was just replaced.
    /// </remarks>
    public void Refresh()
    {
        Completion = null;
        Rebuild();
        Settle(Selection);
    }

    /// <summary>Stops listening to the model's changes.</summary>
    public void Dispose() => History.Dispose();

    private void Rebuild()
    {
        Tree = _builder.Build(Document);
        Layout = _layouter.Layout(Tree);
    }

    /// <summary>Puts the selection onto cells of the current tree, keeping a range whole.</summary>
    /// <remarks>
    /// Both ends are resolved, not just the caret, because the selection belongs to the action: Select All
    /// produces a range and a range that lost its anchor on the way out of the session would be a
    /// highlight the user never sees. Every action that reports an empty selection gets the old behaviour
    /// unchanged - its two ends are the same position, so both resolve to the same cell.
    /// </remarks>
    private void Settle(Selection selection)
    {
        Selection = new Selection(
            CaretNavigation.Nearest(Tree, selection.Anchor),
            CaretNavigation.Nearest(Tree, selection.Caret));
    }

    private static IEnumerable<Node> SelfAndDescendants(Node node)
    {
        yield return node;

        foreach (var child in node.ChildrenInDocumentOrder())
        {
            foreach (var descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private List<string> VariantsOf(Concept concept)
    {
        var variants = new List<string>();

        Collect(concept);
        return variants;

        void Collect(Concept current)
        {
            foreach (var variant in _notations.VariantsOf(current.Key))
            {
                if (!variants.Contains(variant, StringComparer.Ordinal))
                {
                    variants.Add(variant);
                }
            }

            foreach (var super in current.SuperConcepts)
            {
                Collect(super);
            }
        }
    }
}
