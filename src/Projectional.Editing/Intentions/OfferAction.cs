namespace Projectional.Editing;

/// <summary>Alt+Enter or Ctrl+.: offer what can be done where the caret stands.</summary>
/// <remarks>
/// <para>
/// A routing marker, not an edit. Which offers exist at a caret is a question about the session - it needs
/// the model, the tree, the layout, the marker set and the registered sources - and this action deliberately
/// has none of them, so the session answers it. The type exists so that <see cref="KeyRouter"/> can name the
/// gesture without knowing anything else, which is what keeps the routing table headless and testable.
/// </para>
/// <para>
/// What the session then does is not fixed here: it opens a list, and only a lone plain <em>repair</em> runs
/// without one; with nothing to offer it leaves the model and the caret alone. Those are the answers of
/// <see cref="EditorSession.ApplyOffers"/>.
/// </para>
/// </remarks>
public sealed class OfferAction : EditAction
{
    /// <summary>Always throws: the session answers this action and owns everything it needs.</summary>
    /// <param name="context">Unused.</param>
    /// <exception cref="EditingException">Always.</exception>
    public override EditResult Apply(EditContext context) =>
        throw new EditingException(
            "An offer is applied by the session, which owns the model, the sets and the registered sources.");

    public override string ToString() => "offer";
}
