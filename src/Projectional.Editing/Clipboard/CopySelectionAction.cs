namespace Projectional.Editing;

/// <summary>
/// Puts the nodes a selection covers on the clipboard as a fragment.
/// </summary>
/// <remarks>
/// <para>
/// The structural counterpart of <see cref="CopyAction"/>: where a value is selected there is text to copy,
/// and where nodes are selected there is structure to copy. Which nodes is
/// <see cref="SelectionRanges.NodesIn"/>'s question, so this action never decides what a range means - it only
/// asks and copies, exactly as <see cref="DeleteSelectionAction"/> only asks and removes.
/// </para>
/// <para>
/// Writing a fragment replaces whatever the clipboard held, including text: a clipboard carries one payload at
/// a time, and a paste that acted on something the user did not just copy would be a paste of the wrong thing.
/// </para>
/// </remarks>
public sealed class CopySelectionAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="EditOutcome.Unchanged"/> in every case: writing to the clipboard is not a change to the
    /// document, so the host has no tree to rebuild and a copy produces no undo step.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nodes = SelectionRanges.NodesIn(context);

        if (nodes.Count > 0)
        {
            context.Clipboard.Fragment = ModelFragment.Of(nodes);
        }

        return EditResult.Unchanged(context.Selection);
    }

    public override string ToString() => "copy selection";
}
