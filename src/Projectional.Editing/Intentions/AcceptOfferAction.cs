namespace Projectional.Editing;

/// <summary>Enter or a click while an offer list is open: apply the highlighted offer.</summary>
/// <remarks>
/// <para>
/// The list is the session's, so accepting is the session's too, and what it runs goes through
/// <see cref="EditorSession.Apply(Intention)"/> - which routes a repair back through
/// <see cref="EditorSession.Apply(DiagnosticFix)"/>, so a picked repair is still re-resolved against the
/// current set, still one undo step and still rejected with a reason when it no longer holds. Nothing about
/// applying is special-cased for being picked from a list rather than from a menu.
/// </para>
/// <para>
/// A list that has gone away answers unchanged: the user may have chosen with the mouse and had the list
/// closed under the key press, which is not an error.
/// </para>
/// </remarks>
public sealed class AcceptOfferAction : EditAction
{
    /// <summary>Always throws: the session owns the list and applies what it holds.</summary>
    /// <param name="context">Unused.</param>
    /// <exception cref="EditingException">Always.</exception>
    public override EditResult Apply(EditContext context) =>
        throw new EditingException("An offer list is applied by the session, which owns it.");

    public override string ToString() => "accept offer";
}
