using Projectional.Core;
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
/// caret stands, deleting what it can reach, the clipboard group and switching the representation of a
/// concept. Deleting reaches as far as the selection does, so a node selection removes nodes; copying one
/// is still absent because a structural clipboard does not exist yet, and a menu entry that silently did
/// nothing would be worse than no entry at all.
/// </para>
/// <para>
/// The clipboard entries are the same actions the Control-key gestures run, so the gesture an entry
/// prints cannot drift from the binding. Paste reads the clipboard that the session already holds; a
/// host whose clipboard lives in another process refreshes it before the menu is built, exactly as it
/// does before a Ctrl+V.
/// </para>
/// </remarks>
public static class EditorMenu
{
    private const string UndoGesture = "Ctrl+Z";
    private const string RedoGesture = "Ctrl+Y";
    private const string InsertGesture = "Enter";
    private const string DeleteGesture = "Delete";
    private const string SelectAllGesture = "Ctrl+A";
    private const string CopyGesture = "Ctrl+C";
    private const string CutGesture = "Ctrl+X";
    private const string PasteGesture = "Ctrl+V";

    /// <summary>The gesture the quick-fix submenu prints, spelled the way the key map spells it.</summary>
    private const string QuickFixGesture = "Alt+Enter";

    /// <summary>The entries the context menu shows for <paramref name="session"/>.</summary>
    /// <param name="session">The session whose caret and history the menu describes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<EditorMenuEntry> For(EditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var entries = new List<EditorMenuEntry>();

        // The offers first, because they are the group that is about where the caret is rather than about
        // the document as a whole. They are read from one list, so the two submenus agree with the popup
        // about which offers exist and in which order - and so an intention and a repair that name the same
        // operation are listed once, not twice.
        var offers = IntentionList.For(session);
        var fixes = offers.Where(offer => offer.Fix is not null).ToArray();
        var intentions = offers.Where(offer => offer.Fix is null).ToArray();

        if (fixes.Length > 0)
        {
            entries.Add(EditorMenuItem.Submenu(
                "Quick fixes",
                [.. fixes.Select(fix => EditorMenuItem.Run(
                    fix.Label,
                    () => session.Apply(fix),
                    QuickFixGesture,
                    !fix.IsDisabled,
                    TipOf(fix)))]));
        }

        if (intentions.Length > 0)
        {
            entries.Add(EditorMenuItem.Submenu(
                "Intentions",
                [.. intentions.Select(offer => EditorMenuItem.Run(
                    offer.Label,
                    () => session.Apply(offer),
                    offer.Gesture,
                    !offer.IsDisabled,
                    TipOf(offer)))]));
        }

        // One separator after the group, not one per submenu: an empty submenu would be a heading the user
        // has to open to learn there is nothing inside, and a leading separator would separate nothing.
        if (fixes.Length > 0 || intentions.Length > 0)
        {
            entries.Add(EditorMenuSeparator.Instance);
        }

        entries.AddRange(
        [
            // A command says nothing about its outcome: whether an undo found a step to undo is the
            // history's answer, and the entry was already enabled or not by that same history.
            EditorMenuItem.Run("Undo", () => session.Undo(), UndoGesture, session.History.CanUndo),
            EditorMenuItem.Run("Redo", () => session.Redo(), RedoGesture, session.History.CanRedo),
            EditorMenuSeparator.Instance,

            // Folding, when the caret stands on a node that may be folded. It is the one way to fold without
            // a gutter, which is what an embedded editor in a table cell or a reader view needs - and it is
            // here rather than first, because Undo and Redo are what a reader expects at the top.
            .. FoldEntries(session),
            EditorMenuItem.Run(
                "Insert",
                () => session.Apply(new InsertNodeAction()),
                InsertGesture),
            EditorMenuItem.Run(
                "Delete",
                () => session.Apply(KeyRouter.For(session, EditorKey.Delete)),
                DeleteGesture,
                CanDelete(session)),
            EditorMenuSeparator.Instance,
            EditorMenuItem.Run("Select All", () => session.Apply(new SelectAllAction()), SelectAllGesture),
            EditorMenuItem.Run("Copy", () => session.Apply(new CopyAction()), CopyGesture, CanCopyText(session)),
            EditorMenuItem.Run("Cut", () => session.Apply(new CutAction()), CutGesture, CanCopyText(session)),
            EditorMenuItem.Run("Paste", () => session.Apply(new PasteAction()), PasteGesture, CanPasteText(session)),
        ]);

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
    /// Whether Delete has something to remove, which is what makes the entry available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The question is asked of the caret, not of the action, because the action answers it by editing.
    /// Three things can be removed and all three count: a selection inside one cell, a selection that
    /// spans cells (the nodes it covers), and the character to the right of the caret.
    /// </para>
    /// <para>
    /// The entry runs the same routing the Delete key does rather than one fixed action, so the menu and
    /// the keyboard cannot disagree about what Delete means where the caret stands.
    /// </para>
    /// </remarks>
    /// <summary>The fold entry for the caret's node, or nothing when the node cannot be folded.</summary>
    /// <remarks>
    /// A spread rather than an append, so the entry sits in the group it belongs to without the surrounding
    /// list having to be built twice.
    /// </remarks>
    private static EditorMenuEntry[] FoldEntries(EditorSession session)
    {
        if (FoldableNodeOf(session) is not { } foldable)
        {
            return [];
        }

        var folded = session.Collapsed.Contains(foldable.Id);

        return
        [
            EditorMenuItem.Option(
                folded ? "Unfold" : "Fold",
                folded,
                () => session.ToggleCollapsed(foldable.Id)),
            EditorMenuSeparator.Instance,
        ];
    }

    /// <summary>The node the caret stands in when it may be folded, otherwise <see langword="null"/>.</summary>
    /// <remarks>
    /// Resolved through the model rather than the layout: the permission is a model fact, and a folded node
    /// has no other cell to read it from.
    /// </remarks>
    private static Node? FoldableNodeOf(EditorSession session) =>
        session.Document.Model.Project.FindNode(session.Selection.Caret.Address.Owner) is { } node
        && Foldable.Of(node)
            ? node
            : null;

    private static bool CanDelete(EditorSession session)
    {
        var selection = session.Selection;

        if (selection.HasText || selection.SpansCells)
        {
            return true;
        }

        return CaretPlaces.PlaceAt(CaretPlaces.Of(session.Tree), selection.Caret)?.Cell
            is PropertyCell cell
            && selection.Caret.Offset < cell.Text.Length;
    }

    /// <summary>
    /// Whether Copy and Cut have text to take, which is what makes those entries available.
    /// </summary>
    /// <remarks>
    /// A selection that spans two cells is representable but not copyable, so the question is asked of
    /// the same place the actions ask it: the caret must be inside a property cell and the selection must
    /// not be empty. A constant cannot be copied, because its text belongs to the notation and not to
    /// the model.
    /// </remarks>
    private static bool CanCopyText(EditorSession session)
    {
        var selection = session.Selection;

        return selection.HasText
            && CaretPlaces.PlaceAt(CaretPlaces.Of(session.Tree), selection.Caret)?.Cell is PropertyCell;
    }

    /// <summary>
    /// Whether Paste has something to insert, which is what makes that entry available.
    /// </summary>
    /// <remarks>
    /// The clipboard is read, not interrogated: an empty string and no text at all are the same outcome
    /// for the user, and the action refuses both. Pasting into a place that cannot take text is not
    /// checked here - the caret can be anywhere, and the action answers that by leaving the model alone.
    /// </remarks>
    private static bool CanPasteText(EditorSession session) => session.Clipboard.Text is { Length: > 0 };

    /// <summary>The sentence a submenu entry shows under its label.</summary>
    /// <param name="offer">The offer the entry stands for.</param>
    /// <remarks>
    /// A disabled offer shows <em>why</em> rather than what it would do: the user can already see that it
    /// will not run, and the reason is the only part of the message that helps. Everything else shows the
    /// offer's own sentence - which for a quick fix falls back to the finding's message, and for an
    /// intention is required, so a listed entry always has something true to say.
    /// </remarks>
    private static string? TipOf(Intention offer) =>
        offer.IsDisabled ? offer.Availability.Reason : offer.Description;
}
