using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// The cells of a tree the caret can stand in, in the order the notation lays them out.
/// </summary>
/// <remarks>
/// <para>
/// Every leaf is a caret target, including keywords, brackets and spaces: a projectional editor never
/// lets the caret rest in empty space, so clicking the gap between two words has to land on a cell,
/// and the neighbouring cells are what left and right step through. Cells that are not leaves - the
/// containers the notation nests - are never targets, because a caret inside them would have no text
/// to be in. An annotation is a leaf that is not a target either: it is a type hint the projection drew
/// rather than text the author wrote, so the caret steps over it and its neighbours are what left and
/// right reach.
/// </para>
/// <para>
/// Document order is the order in which the builder produced the cells, which is the order in which
/// the notation declares them and therefore the order the reader sees. It is used to break the tie
/// between cells that share an address, and to walk forwards and backwards through the tree, so the
/// lists this class produces follow the notation wherever it puts a cell.
/// </para>
/// </remarks>
internal static class CaretPlaces
{
    /// <summary>The caret targets of <paramref name="tree"/>, in document order.</summary>
    /// <param name="tree">The tree to walk.</param>
    public static IReadOnlyList<CaretPlace> Of(EditorTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        var places = new List<CaretPlace>();
        var order = 0;

        foreach (var cell in tree.Cells())
        {
            if (cell.IsCaretTarget)
            {
                places.Add(new CaretPlace(cell, TextOf(cell), order));
            }

            order++;
        }

        return places;
    }

    /// <summary>The index of the place <paramref name="position"/> means, or <c>-1</c> when there is none.</summary>
    /// <param name="places">The places of one tree.</param>
    /// <param name="position">The position to look for.</param>
    /// <remarks>
    /// Several places can share an address, so a position that remembers its document order picks the
    /// place closest to it; one that does not picks the first place with the address, which is how a
    /// position named by an action is read. When no place carries the address at all - the cell was
    /// removed, or the position names a node rather than the cells inside it - the result is
    /// <c>-1</c> and the caller has to decide what that means.
    /// </remarks>
    public static int Resolve(IReadOnlyList<CaretPlace> places, CaretPosition position)
    {
        ArgumentNullException.ThrowIfNull(places);

        var found = -1;

        for (var index = 0; index < places.Count; index++)
        {
            if (!places[index].Address.Equals(position.Address))
            {
                continue;
            }

            if (position.Order < 0)
            {
                return index;
            }

            if (found < 0 || Distance(places[index], position) < Distance(places[found], position))
            {
                found = index;
            }
        }

        return found;
    }

    /// <summary>The place <paramref name="position"/> means, or <see langword="null"/> when there is none.</summary>
    /// <param name="places">The places of one tree.</param>
    /// <param name="position">The position to look for.</param>
    public static CaretPlace? PlaceAt(IReadOnlyList<CaretPlace> places, CaretPosition position)
    {
        var index = Resolve(places, position);

        return index >= 0 ? places[index] : null;
    }

    /// <summary>The first slot of <paramref name="tree"/>, which is where an empty document starts.</summary>
    /// <param name="tree">The tree to search.</param>
    /// <exception cref="EditingException">The tree has no cell the caret could stand in.</exception>
    public static CaretPosition FirstSlot(EditorTree tree)
    {
        var places = Of(tree);

        foreach (var place in places)
        {
            if (place.IsSlot)
            {
                return CaretPosition.At(place, 0);
            }
        }

        if (places.Count > 0)
        {
            return CaretPosition.At(places[0], 0);
        }

        throw new EditingException("The document has no cell the caret could be placed in.");
    }

    private static int Distance(CaretPlace place, CaretPosition position) =>
        Math.Abs(place.Order - position.Order);

    private static string TextOf(EditorCell cell) => cell is TextCell text ? text.Text : string.Empty;
}
