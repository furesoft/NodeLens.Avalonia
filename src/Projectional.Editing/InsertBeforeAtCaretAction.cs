using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Insert: adds an empty sibling of the node the caret is in, on the side the key means.</summary>
/// <remarks>
/// <para>
/// <see cref="InsertSiblingAction"/> names its node and its side, because a menu entry can say both before the
/// caret is anywhere near. A key means "this one, before it" - so this action is the keystroke's half: it resolves
/// the caret once and delegates, and the refusals stay where they already are.
/// </para>
/// <para>
/// <b>Before, not after.</b> Enter already inserts after the caret's node (<see cref="InsertNodeAction"/> is that
/// key's other half), so a gesture that also inserted after would be a second spelling of Enter. Insert-before is
/// the one place Enter cannot reach, which is why it is the key MPS spends on it.
/// </para>
/// </remarks>
public sealed class InsertBeforeAtCaretAction : EditAction
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        return context.PlaceAt(selection.Caret) is { } place
            ? new InsertSiblingAction(place.Address.Owner, SiblingPlace.Before).Apply(context)
            : EditResult.Unchanged(selection);
    }

    public override string ToString() => "insert before";
}
