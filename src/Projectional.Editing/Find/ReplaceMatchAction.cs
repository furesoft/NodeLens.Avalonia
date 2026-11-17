using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Replaces one occurrence with other text.</summary>
/// <remarks>
/// <para>
/// It replaces the <em>text of a value</em>, not its drawing: the cell keeps its address, the node keeps its identity,
/// and the change is a property write like any other - so undo, marking and the dirty flag need no knowledge of
/// replacement existing.
/// </para>
/// <para>
/// The match is addressed by cell and offsets rather than by the caret, because a search result outlives the caret: the
/// user picks a row, or presses the key again, and the edit must apply where the occurrence was found. The cell is
/// resolved through <see cref="EditContext.PlaceAt"/> with the match's offset, because an address alone does not
/// identify a cell - several cells can share one.
/// </para>
/// <para>
/// A replacement its property type refuses is a rejection with a reason rather than an exception: typing is allowed to
/// fail, so replacing is too, and the reason is what a status line shows.
/// </para>
/// </remarks>
public sealed class ReplaceMatchAction : EditAction
{
    private readonly FindMatch _match;
    private readonly string _replacement;

    /// <summary>Replaces <paramref name="match"/> with <paramref name="replacement"/>.</summary>
    /// <param name="match">The occurrence to replace.</param>
    /// <param name="replacement">What to put there; empty deletes it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is <see langword="null"/>.</exception>
    public ReplaceMatchAction(FindMatch match, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        _match = match;
        _replacement = replacement;
    }

    /// <summary>The occurrence this action replaces.</summary>
    public FindMatch Match => _match;

    /// <summary>What is put in its place.</summary>
    public string Replacement => _replacement;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (CaretNavigation.PlaceAt(context.Tree, CaretPosition.At(_match.Address, _match.Offset)) is not { } place
            || place.Cell is not PropertyCell cell)
        {
            return EditResult.Rejected(selection, "The text this entry replaces is no longer in the document.");
        }

        if (_match.End > cell.Text.Length)
        {
            return EditResult.Rejected(selection, "The text has changed since it was found.");
        }

        var text = string.Concat(cell.Text[.._match.Offset], _replacement, cell.Text[_match.End..]);

        return context.SetProperty(cell, text)
            ? EditResult.Applied(selection)
            : EditResult.Rejected(
                selection,
                $"\"{_replacement}\" is not a valid {cell.Type.Name} value for this slot.");
    }

    public override string ToString() => $"replace {_match} with \"{_replacement}\"";
}