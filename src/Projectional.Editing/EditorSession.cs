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
    private readonly EditorTreeBuilder _builder;
    private readonly CellLayouter _layouter;

    /// <summary>Opens <paramref name="document"/> for editing.</summary>
    /// <param name="document">The root of the model to edit.</param>
    /// <param name="notations">The notation that projects the document's concepts.</param>
    /// <param name="measure">How text is measured; monospace when omitted, which is what tests use.</param>
    /// <exception cref="EditingException">The notation produces no cell a caret can stand in.</exception>
    public EditorSession(Node document, NotationRegistry notations, ITextMeasure? measure = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(notations);

        Document = document;
        Measure = measure ?? MonospaceTextMeasure.Instance;
        History = new UndoHistory(document.Model);

        _builder = new EditorTreeBuilder(notations);
        _layouter = new CellLayouter(Measure);

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

    /// <summary>Applies <paramref name="action"/> and leaves the session consistent with the result.</summary>
    /// <remarks>
    /// The caret the action asks for is resolved against the tree the action produced, so an action may
    /// return an address that only exists after it ran - which is exactly what adding a child needs.
    /// An action that reports no change still gets to move the caret, because that is how the caret
    /// moves at all.
    /// </remarks>
    public EditResult Apply(EditAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var context = new EditContext(Document, Tree, Layout, Measure, Selection);
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

        if (result.Changed)
        {
            Rebuild();
        }

        MoveTo(CaretNavigation.Nearest(Tree, result.Selection.Caret));

        return result;
    }

    /// <summary>Moves the caret, dropping any selection that does not fit the tree.</summary>
    /// <remarks>
    /// Both ends are coerced, so a selection whose anchor was in a deleted node collapses onto the
    /// caret instead of pointing at a cell that no longer exists.
    /// </remarks>
    public void Select(Selection selection)
    {
        var anchor = CaretNavigation.Nearest(Tree, selection.Anchor);
        var caret = CaretNavigation.Nearest(Tree, selection.Caret);

        Selection = new Selection(anchor, caret);
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

    /// <summary>Rebuilds the tree and the layout from the model, then re-resolves the caret.</summary>
    /// <remarks>
    /// For changes the session did not make - a substitution, a background transformation, a test that
    /// wrote to the model directly. The selection is collapsed, because a selection spans cells that may
    /// no longer be there and there is nothing better to do than keep the caret's end.
    /// </remarks>
    public void Refresh()
    {
        Rebuild();
        MoveTo(CaretNavigation.Nearest(Tree, Selection.Caret));
    }

    /// <summary>Stops listening to the model's changes.</summary>
    public void Dispose() => History.Dispose();

    private void Rebuild()
    {
        Tree = _builder.Build(Document);
        Layout = _layouter.Layout(Tree);
    }

    private void MoveTo(CaretPosition caret) => Selection = new Selection(caret, caret);
}
