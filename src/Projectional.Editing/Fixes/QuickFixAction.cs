namespace Projectional.Editing;

/// <summary>Alt+Enter or Ctrl+.: offer what repairs the line the caret is on.</summary>
/// <remarks>
/// <para>
/// A routing marker, not a repair. Which fixes the caret's line offers is a question about the session -
/// it needs the marker set, the fix set, the layout and the measure - and this action deliberately has
/// none of them, so the session answers it. The type exists so that
/// <see cref="KeyRouter"/> can name the gesture without knowing anything else, which is what keeps the
/// routing table headless and testable.
/// </para>
/// <para>
/// What the session then does is not fixed here: with one repair it applies it, with several it opens a
/// list, and with none it leaves the model and the caret alone. Those are the three answers of
/// <see cref="EditorSession.Apply(QuickFixAction)"/>.
/// </para>
/// </remarks>
public sealed class QuickFixAction : EditAction
{
    /// <summary>Always throws: the session answers this action and owns the sets it needs.</summary>
    /// <param name="context">Unused.</param>
    /// <exception cref="EditingException">Always.</exception>
    public override EditResult Apply(EditContext context) =>
        throw new EditingException(
            "A quick fix is applied by the session, which owns the marker set and the fix set.");

    public override string ToString() => "quick fix";
}
