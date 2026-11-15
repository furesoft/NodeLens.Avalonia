using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// What the editor's context menu offers, built from the state of one session.
/// </summary>
/// <remarks>
/// <para>
/// The menu is derived, never stored: <see cref="For"/> runs again for every opening, so every entry
/// describes the caret as it was when the user asked. That is the whole reason the menu lives in this
/// layer rather than in a control - the things it offers are the actions the editing layer already
/// has, and the question "is Undo available" is answered by the history, not by a widget that would
/// have to be told about every edit.
/// </para>
/// <para>
/// It offers what MPS offers at this point of the roadmap: the two history steps, inserting where the
/// caret stands, deleting what it can reach, and switching the representation of a concept. Node
/// deletion and slot-targeted insertion are absent because the actions behind them do not exist yet;
/// a menu entry that silently did nothing would be worse than no entry at all.
/// </para>
/// </remarks>
public static class EditorMenu
{
    private const string UndoGesture = "Ctrl+Z";
    private const string RedoGesture = "Ctrl+Y";
    private const string InsertGesture = "Enter";
    private const string DeleteGesture = "Del";

    /// <summary>The entries the context menu shows for <paramref name="session"/>.</summary>
    /// <param name="session">The session whose caret and history the menu describes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<EditorMenuEntry> For(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var entries = new List<EditorMenuEntry>
        {
            // A command says nothing about its outcome: whether an undo found a step to undo is the
            // history's answer, and the entry was already enabled or not by that same history.
            EditorMenuItem.Run("Undo", () => session.Undo(), UndoGesture, session.History.CanUndo),
            EditorMenuItem.Run("Redo", () => session.Redo(), RedoGesture, session.History.CanRedo),
            EditorMenuSeparator.Instance,
            EditorMenuItem.Run(
                "Insert",
                () => session.Apply(new InsertNodeAction()),
                InsertGesture),
            EditorMenuItem.Run(
                "Delete",
                () => session.Apply(new DeleteTextAction(DeleteDirection.Forward)),
                DeleteGesture,
                CanDeleteText(session)),
        };

        var representations = Representations(session);

        if (representations.Count > 0)
        {
            entries.Add(EditorMenuSeparator.Instance);
            entries.Add(EditorMenuItem.Submenu("Representation", representations));
        }

        return entries;
    }

    /// <summary>The representations the document offers, one submenu per concept that has a choice.</summary>
    /// <param name="session">The session whose alternatives the menu describes.</param>
    /// <remarks>
    /// A host that shows representations in a menu bar rather than in the context menu wants exactly
    /// this list, which is why it is public: the choice is a property of the document, and both menus
    /// have to offer the same one.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<EditorMenuEntry> Representations(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var entries = new List<EditorMenuEntry>();

        foreach (var alternative in session.Alternatives())
        {
            var variants = new List<EditorMenuEntry>();

            foreach (var variant in alternative.Variants)
            {
                var conceptKey = alternative.ConceptKey;
                var name = variant;

                variants.Add(EditorMenuItem.Option(
                    variant,
                    string.Equals(variant, alternative.Selected, StringComparison.Ordinal),
                    () => session.ChooseVariant(conceptKey, name)));
            }

            entries.Add(EditorMenuItem.Submenu(alternative.ConceptKey, variants));
        }

        return entries;
    }

    /// <summary>
    /// Whether Delete has a character to remove, which is what makes the entry available.
    /// </summary>
    /// <remarks>
    /// The question is asked of the caret, not of the action, because the action answers it by editing:
    /// forward delete needs a character to the right, and outside a property there is no text at all.
    /// A selection counts even when the caret sits at the end of its cell, since deleting the selection
    /// does not care where the caret is.
    /// </remarks>
    private static bool CanDeleteText(EditorSession session)
    {
        var selection = session.Selection;

        if (selection.HasText)
        {
            return true;
        }

        return CaretPlaces.PlaceAt(CaretPlaces.Of(session.Tree), selection.Caret)?.Cell
            is PropertyCell cell
            && selection.Caret.Offset < cell.Text.Length;
    }
}
