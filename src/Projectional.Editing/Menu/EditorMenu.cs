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
/// concept. Deleting reaches as far as the selection does, so a node selection removes nodes; copying and
/// cutting do the same in the other direction - a selection that covers nodes copies them as a fragment,
/// while a selection inside one value copies its text.
/// </para>
/// <para>
/// The clipboard entries run the same routing the Control-key gestures do (<see cref="KeyRouter"/>), so the
/// gesture an entry prints cannot drift from the binding, and an entry cannot be available while its key
/// would do something else.
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
    private const string CompletionGesture = "Ctrl+Space";
    private const string DeleteGesture = "Delete";
    private const string SelectAllGesture = "Ctrl+A";
    private const string CopyGesture = "Ctrl+C";
    private const string CutGesture = "Ctrl+X";
    private const string PasteGesture = "Ctrl+V";

    /// <summary>The gesture the quick-fix submenu prints, spelled the way the key map spells it.</summary>
    private const string QuickFixGesture = "Alt+Enter";

    /// <summary>The gesture the go-to-definition entry prints, spelled the way the key map spells it.</summary>
    private const string GoToDefinitionGesture = "Ctrl+B";

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
            InsertEntry(session),
            EditorMenuItem.Run(
                "Insert...",
                () => session.Apply(new OpenCompletionAction()),
                CompletionGesture,
                CanComplete(session),
                "Pick what goes here from what the language allows, the list Ctrl+Space opens."),
            EditorMenuItem.Run(
                "Delete",
                () => session.Apply(KeyRouter.For(session, EditorKey.Delete)),
                DeleteGesture,
                CanDelete(session)),
            EditorMenuSeparator.Instance,
            EditorMenuItem.Run("Select All", () => session.Apply(new SelectAllAction()), SelectAllGesture),
            EditorMenuItem.Run("Copy", () => session.Apply(KeyRouter.For(session, EditorKey.Copy)), CopyGesture, CanCopy(session)),
            EditorMenuItem.Run("Cut", () => session.Apply(KeyRouter.For(session, EditorKey.Cut)), CutGesture, CanCopy(session)),
            EditorMenuItem.Run("Paste", () => session.Apply(KeyRouter.For(session, EditorKey.Paste)), PasteGesture, CanPaste(session)),
            EditorMenuItem.Run(
                "Go to Definition",
                () => session.Apply(new GoToDefinitionAction()),
                GoToDefinitionGesture,
                GoToDefinition.CanNavigate(session),
                "Move the caret to the declaration the reference here points at."),
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

    /// <summary>
    /// The entry that does what Enter does, named after what it will create when that is knowable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `Insert "Step"` rather than `Insert`: the concept a slot is declared over is usually a set, and which
    /// member of it Enter picks is the language's answer (<see cref="ConceptRegistry.DefaultInstantiableFor"/>),
    /// so a bare "Insert" would make the user press it to find out. The label is derived from the same call the
    /// action makes, so the two cannot disagree.
    /// </para>
    /// <para>
    /// It is disabled rather than hidden where Enter would do nothing - the caret is in no slot and its node has
    /// no list to grow - because a row that vanishes and reappears as the caret moves is harder to find than one
    /// that says why it cannot run. Where the caret is inside a node that fills a list, it keeps its own name:
    /// there Enter adds a sibling of the same concept, which has no "set" to pick from.
    /// </para>
    /// </remarks>
    private static EditorMenuItem InsertEntry(EditorSession session)
    {
        var context = session.ContextFor(new InsertNodeAction());
        var place = CaretPlaces.PlaceAt(CaretPlaces.Of(session.Tree), session.Selection.Caret);

        if (place?.Cell is ChildCell { IsEmpty: true } child)
        {
            return EmptySlotEntry(context, child.Slot, session);
        }

        if (place?.Cell is CollectionCell { IsEmpty: true } collection)
        {
            return EmptySlotEntry(context, collection.Slot, session);
        }

        if (place?.Cell is AddRowCell row)
        {
            return EmptySlotEntry(context, row.Slot, session);
        }

        var grows = session.Selection.Caret.Address is { Owner: var owner }
            && context.NodeOf(owner) is { ParentSlot: { } slot } node
            && node.Parent?.Concept.FindChild(slot) is { Cardinality: Cardinality.Many };

        return EditorMenuItem.Run(
            "Insert",
            () => session.Apply(new InsertNodeAction()),
            InsertGesture,
            grows,
            grows ? null : "Enter adds a sibling only inside a list.");
    }

    /// <summary>The entry for an empty slot: named after the concept Enter would create there.</summary>
    private static EditorMenuItem EmptySlotEntry(EditContext context, string slot, EditorSession session)
    {
        var concept = context.NodeOf(session.Selection.Caret.Address.Owner) is { Concept: var owner }
            ? context.Concepts.DefaultInstantiableFor(owner.FindChild(slot)?.ChildConceptKey ?? string.Empty)
            : null;

        return EditorMenuItem.Run(
            concept is null ? "Insert" : $"Insert \"{CompletionItem.LabelOf(concept.Key)}\"",
            () => session.Apply(new InsertNodeAction()),
            InsertGesture,
            concept is not null,
            concept is null ? "No concept of the language can stand in this slot." : null);
    }

    /// <summary>
    /// Whether completion would open at this caret, asked of the one thing that decides it - the entry would
    /// otherwise offer a list that never appears.
    /// </summary>
    private static bool CanComplete(EditorSession session) =>
        CompletionSlot.At(session.ContextFor(new OpenCompletionAction())) is not null;

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
    /// Whether Copy and Cut have something to take, which is what makes those entries available.
    /// </summary>
    /// <remarks>
    /// Two cases, and the entries are simply unavailable when neither holds: text inside one property cell, and
    /// a selection that covers nodes. The predicate asks the same questions the actions do - the text case wants
    /// the caret in a <see cref="PropertyCell"/>, and the structural case is <see cref="KeyRouter.CopiesNodes"/>,
    /// which is what the gesture itself consults. A constant can be copied in neither sense, because its text
    /// belongs to the notation and not to the model.
    /// </remarks>
    private static bool CanCopy(EditorSession session)
    {
        var selection = session.Selection;

        return (selection.HasText
                && CaretPlaces.PlaceAt(CaretPlaces.Of(session.Tree), selection.Caret)?.Cell is PropertyCell)
            || KeyRouter.CopiesNodes(session);
    }

    /// <summary>
    /// Whether Paste has something to insert, which is what makes that entry available.
    /// </summary>
    /// <remarks>
    /// The clipboard is read, not interrogated: text that is empty and no text at all are the same outcome for
    /// the user, and the actions refuse both. A fragment counts even though it carries no text, because pasting
    /// one creates nodes. Whether the caret is somewhere a fragment can go is not checked here - the caret can
    /// be anywhere, and the action answers that itself.
    /// </remarks>
    private static bool CanPaste(EditorSession session) =>
        session.Clipboard.Fragment is { IsEmpty: false } || session.Clipboard.Text is { Length: > 0 };

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
