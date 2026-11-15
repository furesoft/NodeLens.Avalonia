using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Selects the property the caret is in, then the node it is in, then the whole document.
/// </summary>
/// <remarks>
/// <para>
/// A projectional document nests three units, and "everything" is ambiguous between them. So Ctrl+A
/// widens one step per press: the value first, because that is what the next keystroke replaces and what
/// Copy takes; the node next, because that is what Delete removes and Alt+Up moves; the document last,
/// because that is what a host saves or copies as a whole. Each step is a range over caret places, so
/// nothing below this action needs to know the gesture exists.
/// </para>
/// <para>
/// A step is skipped when it has nothing to offer rather than when it is inconvenient: a caret in a
/// keyword has no value, so the first press selects the node; a node whose notation draws a single
/// property produces the same range as that property, so the second press reaches the document. The
/// ladder therefore never stalls, and the last press is a no-op - the document is already selected.
/// </para>
/// <para>
/// Widening past the value is deliberately not what a text editor does, because the units here are not
/// text: selecting a whole node is a selection of a <em>thing</em>, which is why <see cref="Selection"/>
/// already allowed a range across cells before any gesture produced one.
/// </para>
/// </remarks>
public sealed class SelectAllAction : EditAction
{
    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="EditOutcome.Unchanged"/>, because the model is untouched and only the selection moves -
    /// the same outcome <see cref="MoveCaretAction"/> reports for the same reason.
    /// </remarks>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (SelectionRanges.OfProperty(context) is { } property && !AlreadySelected(selection, property))
        {
            return EditResult.Unchanged(property);
        }

        var node = context.PlaceAt(selection.Caret) is { } place ? context.NodeOf(place.Address) : null;

        if (node is not null &&
            SelectionRanges.OfNode(context, node) is { } subtree &&
            !AlreadySelected(selection, subtree))
        {
            return EditResult.Unchanged(subtree);
        }

        if (SelectionRanges.OfDocument(context) is { } document && !AlreadySelected(selection, document))
        {
            return EditResult.Unchanged(document);
        }

        return EditResult.Unchanged(selection);
    }

    /// <summary>Whether <paramref name="selection"/> is already exactly <paramref name="range"/>.</summary>
    /// <remarks>
    /// The comparison ignores <see cref="CaretPosition.Order"/> on purpose. A position named by address
    /// alone carries no document order, so record equality would call the same range different and the
    /// ladder would widen a step the user has already taken.
    /// </remarks>
    private static bool AlreadySelected(Selection selection, Selection range) =>
        SamePlace(selection.Anchor, range.Anchor) && SamePlace(selection.Caret, range.Caret);

    private static bool SamePlace(CaretPosition left, CaretPosition right) =>
        left.Address.Equals(right.Address) && left.Offset == right.Offset;

    public override string ToString() => "select all";
}
