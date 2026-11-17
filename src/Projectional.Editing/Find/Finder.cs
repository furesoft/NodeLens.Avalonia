using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Finds text in a document, in the order a reader would meet it.</summary>
/// <remarks>
/// <para>
/// The search walks the caret places rather than the model, because the question is "where is this text on the page"
/// and only the projection knows that. A property that a notation hides cannot be found, which is the same rule the
/// caret obeys - a user searching a document they are looking at should not jump somewhere they cannot see.
/// </para>
/// <para>
/// Only text a user can change is searched: property cells. A keyword or a parenthesis is drawn from the notation
/// rather than from the document, so finding one would mean offering to replace something no edit can touch.
/// </para>
/// <para>
/// The result is a list, not an iterator: a caller that walks it twice must not pay for the walk twice, and the list is
/// stale the moment the model changes - which is why nothing here is cached between calls.
/// </para>
/// </remarks>
public static class Finder
{
    /// <summary>Every occurrence of <paramref name="query"/>, in document order.</summary>
    /// <param name="session">The session whose document is searched.</param>
    /// <param name="query">What to look for.</param>
    public static IReadOnlyList<FindMatch> Find(EditorSession session, FindQuery query)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(query);

        return [.. Scan(session.Tree, query).Select(match => new FindMatch(match.Place.Address, match.Offset, match.Length))];
    }

    /// <summary>Every occurrence, as the places and offsets the caret moves over.</summary>
    /// <param name="tree">The tree to search.</param>
    /// <param name="query">What to look for.</param>
    internal static IReadOnlyList<TextMatch> Scan(EditorTree tree, FindQuery query)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(query);

        if (query.IsEmpty)
        {
            return [];
        }

        List<TextMatch> matches = [];

        foreach (var place in CaretPlaces.Of(tree))
        {
            if (place.Cell is not PropertyCell cell || cell.Text.Length == 0)
            {
                continue;
            }

            for (var start = cell.Text.IndexOf(query.Text, query.Comparison);
                start >= 0;
                start = cell.Text.IndexOf(query.Text, start + query.Text.Length, query.Comparison))
            {
                matches.Add(new TextMatch(place, start, query.Text.Length));

                // A zero-length query cannot occur, so the loop always advances - but the length is a
                // guarantee, not an assumption: an overlapping match ("aa" in "aaa") is not reported twice.
                if (query.Text.Length == 0)
                {
                    break;
                }
            }
        }

        return matches;
    }

    /// <summary>The next occurrence in <paramref name="direction"/>, wrapping around.</summary>
    /// <param name="tree">The tree to search.</param>
    /// <param name="selection">Where the caret stands now.</param>
    /// <param name="query">What to look for.</param>
    /// <remarks>
    /// The caret's own occurrence is not a candidate: pressing "next" twice must move twice, so a match the caret is
    /// already inside is passed over. That is also what makes a repeated search walk the document instead of stalling
    /// on the first hit.
    /// </remarks>
    internal static TextMatch? Next(EditorTree tree, Selection selection, FindQuery query, FindDirection direction)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(query);

        var matches = Scan(tree, query);

        if (matches.Count == 0)
        {
            return null;
        }

        var places = CaretPlaces.Of(tree);
        var at = CaretPlaces.PlaceAt(places, selection.Caret);
        var order = at?.Order ?? (direction == FindDirection.Forward ? -1 : places.Count);

        // Matches do not overlap, so "after the caret" is exactly "starts at or after the end of what is
        // selected" within the caret's own cell - and "before" is "ends at or before its start".
        if (direction == FindDirection.Forward)
        {
            foreach (var match in matches)
            {
                if (IsCurrent(match, selection))
                {
                    continue;
                }

                if (match.Place.Order > order
                    || (match.Place.Order == order && match.Offset >= selection.End))
                {
                    return match;
                }
            }

            return matches[0];
        }

        for (var index = matches.Count - 1; index >= 0; index--)
        {
            if (IsCurrent(matches[index], selection))
            {
                continue;
            }

            if (matches[index].Place.Order < order
                || (matches[index].Place.Order == order
                    && matches[index].Offset + matches[index].Length <= selection.Start))
            {
                return matches[index];
            }
        }

        return matches[^1];
    }

    /// <summary>Whether <paramref name="match"/> is exactly what is selected now.</summary>
    /// <remarks>
    /// What the previous press selected is not a candidate: pressing the key again must move, or the search appears
    /// stuck on the first hit it found. A zero-length selection cannot equal a match, so a bare caret is unaffected.
    /// </remarks>
    private static bool IsCurrent(TextMatch match, Selection selection) =>
        selection.HasText
        && match.Place.Address.Equals(selection.Caret.Address)
        && match.Offset == selection.Start
        && match.Length == selection.End - selection.Start;
}
