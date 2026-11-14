using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One editing step, expressed as a value that can be applied rather than as a mutation.
/// </summary>
/// <remarks>
/// <para>
/// Every keyboard shortcut, and later every substitution and every programmatic edit, is an action
/// object. The point is not ceremony: an action can be built without a UI, applied in a test, described
/// in a log and replayed, and it gives the layer one place where the model is allowed to change. The
/// projection architecture freezes "input never mutates the model directly, it applies an edit action",
/// and this type is that sentence.
/// </para>
/// <para>
/// An action never touches the tree, the layout or a cell's children. It may only set a property or add
/// a child through <see cref="EditContext"/>, so the model's own validation and its change journal stay
/// the single source of truth, and undo keeps working without the editing layer knowing about it.
/// </para>
/// </remarks>
public abstract class EditAction
{
    /// <summary>
    /// The key that consecutive commits should be grouped under, or <see langword="null"/> when this
    /// edit must stay an undo step of its own.
    /// </summary>
    /// <remarks>
    /// A burst of typing has to become one undo press, so the key is derived from the place being
    /// edited: two keystrokes in the same slot share it and merge, while a keystroke after a caret move
    /// - or in another slot - does not. The policy lives with the action because the action is what
    /// knows what its burst is; the session only has to hand the key to the history.
    /// </remarks>
    public virtual string? CoalescingKey(EditContext context) => null;

    /// <summary>Applies the action and answers with what happened and where the caret goes.</summary>
    public abstract EditResult Apply(EditContext context);

    /// <summary>
    /// A stable name for one place in the model, for use as a coalescing key.
    /// </summary>
    /// <remarks>
    /// <see cref="CellAddress.ToString"/> is not enough because it only spells the slot, and two nodes
    /// with the same slot name would collide - typing in one rule's name would merge with typing in
    /// another's.
    /// </remarks>
    protected static string KeyOf(CellAddress address) =>
        $"{address.Owner}/{address.Slot ?? "<node>"}/{address.Index}";

    public override string ToString() => GetType().Name;
}
