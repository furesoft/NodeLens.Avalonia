namespace Projectional.Editing;

/// <summary>
/// Copies the nodes a selection covers to the clipboard and then removes them.
/// </summary>
/// <remarks>
/// <para>
/// Cut is copy followed by delete, and it delegates both - the same rule <see cref="CutAction"/> follows for
/// text. Writing the fragment again here would be a second answer to "what did the user select", and removing
/// the nodes by hand would be a second answer to "what does a range mean" including where the caret lands.
/// </para>
/// <para>
/// The copy happens first, so a removal that happens to be refused leaves the clipboard holding what the user
/// asked for. That is the honest outcome: they can paste it somewhere it fits.
/// </para>
/// </remarks>
public sealed class CutSelectionAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// The result of the deletion, so the caret lands where removing the range leaves it.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nodes = SelectionRanges.NodesIn(context);

        if (nodes.Count == 0)
        {
            return EditResult.Unchanged(context.Selection);
        }

        context.Clipboard.Fragment = ModelFragment.Of(nodes);

        return new DeleteSelectionAction().Apply(context);
    }

    /// <summary>Never coalesces, so a cut is one undo step of its own.</summary>
    public override string? CoalescingKey(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return null;
    }

    public override string ToString() => "cut selection";
}
