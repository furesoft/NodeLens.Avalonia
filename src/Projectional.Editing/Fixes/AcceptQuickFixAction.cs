namespace Projectional.Editing;

/// <summary>Enter or a click while a quick-fix list is open: apply the highlighted repair.</summary>
/// <remarks>
/// <para>
/// The list is the session's, so accepting is the session's too, and the repair it runs goes through
/// <see cref="EditorSession.Apply(DiagnosticFix)"/> like any other pick - re-resolved against the current
/// set, one undo step, rejected with a reason when it no longer resolves. Nothing about applying a repair
/// is special-cased for being picked from a list rather than from a menu.
/// </para>
/// <para>
/// A list that has gone away answers unchanged: the user may have chosen with the mouse and had the list
/// closed under the key press, which is not an error.
/// </para>
/// </remarks>
public sealed class AcceptQuickFixAction : EditAction
{
    /// <summary>Always throws: the session owns the list and applies the repair it holds.</summary>
    /// <param name="context">Unused.</param>
    /// <exception cref="EditingException">Always.</exception>
    public override EditResult Apply(EditContext context) =>
        throw new EditingException("A quick-fix list is applied by the session, which owns it.");

    public override string ToString() => "accept quick fix";
}
