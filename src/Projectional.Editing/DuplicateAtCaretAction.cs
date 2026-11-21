using Projectional.Core;

namespace Projectional.Editing;

/// <summary>Ctrl+D: copies the node the caret is in and everything below it, right after it.</summary>
/// <remarks>
/// <para>
/// <see cref="DuplicateNodeAction"/> names its node, because a menu entry or a repair has to say what it acts on
/// before anybody's caret is there. A keystroke cannot: it means "this one", and "this" is wherever the caret is when
/// the key is pressed. So this action is the keystroke's half - it resolves the caret once and delegates, which keeps
/// the refusals ("no place", "the only child of a single-valued slot") in one place instead of two.
/// </para>
/// <para>
/// The node is the one the caret's cell belongs to, the same answer <see cref="InsertNodeAction"/> uses for "the
/// node the caret is in": a caret on a property duplicates its owner, not the property, because a property has no
/// identity of its own in the tree.
/// </para>
/// </remarks>
public sealed class DuplicateAtCaretAction : EditAction
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        return context.PlaceAt(selection.Caret) is { } place
            ? new DuplicateNodeAction(place.Address.Owner).Apply(context)
            : EditResult.Unchanged(selection);
    }

    public override string ToString() => "duplicate";
}
