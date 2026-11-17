namespace Projectional.Editing;

/// <summary>Selects the next occurrence of a query, starting at the caret and wrapping around.</summary>
/// <remarks>
/// <para>
/// It is a navigation, like <see cref="SelectSlotAction"/>: the model is untouched, so it creates no undo step, and the
/// answer is the selection of the occurrence. That is what makes "find next" repeatable - press it again and the caret
/// has moved, so the next press finds the following occurrence.
/// </para>
/// <para>
/// The caret's own occurrence is passed over rather than selected again, which is why a repeated press walks the
/// document instead of stalling on the first hit. Wrapping is not a special case spelled here: when nothing lies after
/// the caret the first occurrence is the next one, and the same holds at the start for a backward search.
/// </para>
/// <para>
/// A query that matches nothing answers <see cref="EditResult.Unchanged"/> with the caret where it was. "Not found" is
/// an ordinary outcome of a search, not a rejection - a UI that wants to say so counts the matches itself, which is one
/// call to <see cref="Finder.Find"/>.
/// </para>
/// </remarks>
public sealed class FindNextAction : EditAction
{
    private readonly FindQuery _query;
    private readonly FindDirection _direction;

    /// <summary>Finds the occurrence after (or before) the caret.</summary>
    /// <param name="query">What to look for.</param>
    /// <param name="direction">Which way to search; forward when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    public FindNextAction(FindQuery query, FindDirection direction = FindDirection.Forward)
    {
        ArgumentNullException.ThrowIfNull(query);

        _query = query;
        _direction = direction;
    }

    /// <summary>What is looked for.</summary>
    public FindQuery Query => _query;

    /// <summary>Which way the search walks.</summary>
    public FindDirection Direction => _direction;

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override EditResult Apply(EditContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var selection = context.Selection;

        if (Finder.Next(context.Tree, selection, _query, _direction) is not { } match)
        {
            return EditResult.Unchanged(selection);
        }

        return EditResult.Unchanged(
            new Selection(
                CaretPosition.At(match.Place, match.Offset),
                CaretPosition.At(match.Place, match.Offset + match.Length)));
    }

    public override string ToString() =>
        $"find {(_direction == FindDirection.Forward ? "next" : "previous")} \"{_query.Text}\"";
}