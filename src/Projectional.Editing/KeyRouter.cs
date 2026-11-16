using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Turns a key into the edit action it stands for.
/// </summary>
/// <remarks>
/// <para>
/// The routing table is a public function rather than a method on the session so that a host can ask
/// what a key would do without a document open - to build a menu, to describe a shortcut, or to decide
/// whether to swallow the key at all. It is the single place where the mapping lives, so a control that
/// embeds the editor never has to reimplement it.
/// </para>
/// <para>
/// An unknown key throws. That is intended: the enum lists exactly what the layer handles, so a value
/// outside it means the caller either cast something it should not have or has a newer key than this
/// table, and both are programming errors that a silent <see langword="null"/> would hide.
/// </para>
/// </remarks>
public static class KeyRouter
{
    /// <summary>The action <paramref name="key"/> stands for.</summary>
    /// <remarks>
    /// Enter is routed as an insertion here, which is what the key does wherever the caret is not in a
    /// value of text. Backspace and Delete are routed as text deletes, which is what they do wherever
    /// there is text to delete. A host with a document open should ask
    /// <see cref="For(EditorSession, EditorKey)"/> instead, because only that one can tell the two cases
    /// apart and reach the node behind them.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is not a routed key.</exception>
    public static EditAction For(EditorKey key) => key switch
    {
        EditorKey.Left => new MoveCaretAction(CaretMove.Left),
        EditorKey.Right => new MoveCaretAction(CaretMove.Right),
        EditorKey.ControlLeft => new MoveCaretAction(CaretMove.LeftCharacter),
        EditorKey.ControlRight => new MoveCaretAction(CaretMove.RightCharacter),
        EditorKey.Up => new MoveCaretAction(CaretMove.Up),
        EditorKey.Down => new MoveCaretAction(CaretMove.Down),
        EditorKey.AltUp => new MoveNodeAction(NodeMove.Up),
        EditorKey.AltDown => new MoveNodeAction(NodeMove.Down),
        EditorKey.Home => new MoveCaretAction(CaretMove.LineStart),
        EditorKey.ControlHome => new MoveCaretAction(CaretMove.DocumentStart),
        EditorKey.End => new MoveCaretAction(CaretMove.LineEnd),
        EditorKey.ControlEnd => new MoveCaretAction(CaretMove.DocumentEnd),
        EditorKey.PageUp => new MoveCaretAction(CaretMove.PageUp),
        EditorKey.PageDown => new MoveCaretAction(CaretMove.PageDown),
        EditorKey.Tab => new TabAction(),
        EditorKey.ShiftTab => new TabAction(backward: true),
        EditorKey.Enter => new InsertNodeAction(),
        EditorKey.Backspace => new DeleteTextAction(DeleteDirection.Backward),
        EditorKey.Delete => new DeleteTextAction(DeleteDirection.Forward),
        EditorKey.ControlSpace => new OpenCompletionAction(),
        EditorKey.Escape => new DismissCompletionAction(),
        EditorKey.SelectAll => new SelectAllAction(),
        EditorKey.Copy => new CopyAction(),
        EditorKey.Cut => new CutAction(),
        EditorKey.Paste => new PasteAction(),
        EditorKey.QuickFix => new QuickFixAction(),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown editor key."),
    };

    /// <summary>The action <paramref name="key"/> stands for, given where the caret currently is.</summary>
    /// <param name="session">The session whose caret and completion list decide what the key means.</param>
    /// <param name="key">The key that was pressed.</param>
    /// <remarks>
    /// <para>
    /// A key is not always the same action, and the difference needs state this table cannot see alone.
    /// Enter opens a line in the text of a property and inserts a node everywhere else; while a
    /// completion list is open, Enter and Tab accept the highlighted proposal, the arrows move the
    /// highlight, and Backspace drops the last filter character instead of deleting text.
    /// </para>
    /// <para>
    /// Everything else falls through to the plain table and therefore closes an open list, because a
    /// non-completion action is exactly "the user did something else". That is one rule instead of a
    /// list of keys to close on.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is not a routed key.</exception>
    public static EditAction For(EditorSession session, EditorKey key)
    {
        ArgumentNullException.ThrowIfNull(session);

        // An open quick-fix list is asked first: it was opened by a deliberate gesture on a line that has
        // repairs, so while it is up the arrows and Enter belong to it. Escape is shared - it closes
        // whichever list is open, which is what one gesture for "never mind" means.
        if (session.OfferedFixes is not null && QuickFixListed(key) is { } quickFix)
        {
            return quickFix;
        }

        if (session.Completion is { } list && Listed(list, key) is { } listed)
        {
            return listed;
        }

        return key switch
        {
            EditorKey.Enter when IsTextPlace(session) => new InsertLineBreakAction(),
            EditorKey.Backspace => Delete(session, DeleteDirection.Backward),
            EditorKey.Delete => Delete(session, DeleteDirection.Forward),
            _ => For(key),
        };
    }

    /// <summary>The delete <paramref name="key"/> means where the caret currently is.</summary>
    /// <remarks>
    /// A selection that spans cells reaches first, because it is the most specific thing the user has
    /// said: it names nodes, and <see cref="DeleteSelectionAction"/> removes exactly those. Only a caret
    /// reaches for text, and only where there is any: inside a value a character goes, and at a cell
    /// boundary the node does, because the neighbouring cell is a node and there is nothing smaller to
    /// take away.
    /// </remarks>
    private static EditAction Delete(EditorSession session, DeleteDirection direction) =>
        session.Selection.SpansCells ? new DeleteSelectionAction()
        : RemovesText(session, direction) ? new DeleteTextAction(direction)
        : new DeleteNodeAction();

    /// <summary>Whether a text delete in <paramref name="direction"/> has something to remove.</summary>
    private static bool RemovesText(EditorSession session, DeleteDirection direction)
    {
        var selection = session.Selection;

        if (CaretNavigation.PlaceAt(session.Tree, selection.Caret)?.Cell is not PropertyCell cell)
        {
            return false;
        }

        if (selection.HasText)
        {
            return true;
        }

        var offset = Math.Clamp(selection.Caret.Offset, 0, cell.Text.Length);

        return direction == DeleteDirection.Backward
            ? offset > 0
            : offset < cell.Text.Length;
    }

    /// <summary>What <paramref name="key"/> means while a quick-fix list is open, if anything.</summary>
    /// <remarks>
    /// Up and Down move the highlight rather than the caret, so a list that is up cannot be navigated past
    /// and lost. Enter and Tab apply the highlighted repair - Tab as well, because that is what accepts a
    /// completion and a user who has learned one list has learned both.
    /// </remarks>
    private static EditAction? QuickFixListed(EditorKey key) => key switch
    {
        EditorKey.Up => new SelectQuickFixAction(-1),
        EditorKey.Down => new SelectQuickFixAction(1),
        EditorKey.Enter or EditorKey.Tab => new AcceptQuickFixAction(),
        EditorKey.Escape => new DismissCompletionAction(),
        _ => null,
    };

    /// <summary>What <paramref name="key"/> means while <paramref name="list"/> is open, if anything.</summary>
    private static EditAction? Listed(CompletionList list, EditorKey key) => key switch
    {
        EditorKey.Up => new SelectCompletionAction(-1),
        EditorKey.Down => new SelectCompletionAction(1),
        EditorKey.Enter or EditorKey.Tab => new AcceptCompletionAction(),
        EditorKey.Escape => new DismissCompletionAction(),
        EditorKey.Backspace when list.Prefix.Length > 0 => new WidenCompletionAction(),
        _ => null,
    };

    /// <summary>Whether the caret of <paramref name="session"/> stands in text that can hold a line.</summary>
    /// <remarks>
    /// Editable is not enough: an integer or a date is typed but not broken, and a break in one of those
    /// would be a rejected edit the user never asked for. The property has to be a text, which is the
    /// only type whose value can contain a line.
    /// </remarks>
    private static bool IsTextPlace(EditorSession session) =>
        CaretNavigation.PlaceAt(session.Tree, session.Selection.Caret)?.Cell is PropertyCell cell
        && cell.Type.Equals(PropertyType.String);

    /// <summary>The action for typed text, which is one burst of characters.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static EditAction Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new TypeTextAction(text);
    }

    /// <summary>The action for typed text, given whether a completion list is open.</summary>
    /// <param name="session">The session whose completion list decides what the text is for.</param>
    /// <param name="text">The characters that arrived.</param>
    /// <remarks>
    /// While a list is open the characters filter it instead of being typed, which is why this overload
    /// exists at all: the caret is standing in an empty slot, so there is nowhere to put a character,
    /// and the user is spelling out the concept they want.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="session"/> or <paramref name="text"/> is <see langword="null"/>.
    /// </exception>
    public static EditAction Type(EditorSession session, string text)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.Completion is not null ? new NarrowCompletionAction(text) : Type(text);
    }
}
