namespace Projectional.Editing;

/// <summary>Up or Down while a quick-fix list is open: move the highlight, change nothing else.</summary>
/// <remarks>
/// The same shape as <see cref="SelectCompletionAction"/>, and for the same reason: moving a highlight is
/// not an edit, so it cannot be a property of the model and must not touch the history. The session owns
/// the list, so the session moves the highlight - the control only says which way the key pointed.
/// </remarks>
public sealed class SelectQuickFixAction : EditAction
{
    private readonly int _delta;

    /// <summary>Moves the highlight by <paramref name="delta"/> rows.</summary>
    /// <param name="delta">How many rows to move, negative towards the front.</param>
    public SelectQuickFixAction(int delta) => _delta = delta;

    /// <summary>How many rows the highlight moves.</summary>
    public int Delta => _delta;

    /// <summary>Always throws: the session owns the list this action moves in.</summary>
    /// <param name="context">Unused.</param>
    /// <exception cref="EditingException">Always.</exception>
    public override EditResult Apply(EditContext context) =>
        throw new EditingException("A quick-fix list is moved by the session, which owns it.");

    public override string ToString() => $"select quick fix {_delta:+#;-#;0}";
}
