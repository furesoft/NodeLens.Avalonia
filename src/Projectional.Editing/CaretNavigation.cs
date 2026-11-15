using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Moves the caret through the cells of a laid-out tree.
/// </summary>
/// <remarks>
/// <para>
/// Horizontal movement is structural: it steps from one cell to the next in document order, so the
/// caret never lands in the gaps that alignment leaves between cells, and one press left at the start
/// of a keyword continues at the end of whatever the notation put before it. A cell can be several
/// characters long - a property usually is - so the character-wise moves stay available as their own
/// moves for the host that wants to let the user fix a typo in the middle of one. Vertical movement is
/// geometric instead, because there is no structure that says which cell is above which - lines are a
/// result of layout, not of the notation. It therefore looks for the cell on the nearest line in the
/// requested direction and keeps the column as well as it can, which is what a reader expects and
/// what makes the caret feel like it is moving through text rather than through a tree.
/// </para>
/// <para>
/// Nothing here edits. A move produces a new position and leaves every other decision - clamping it
/// into the tree, rebuilding, redrawing - to the session, so navigation stays a pure function that
/// can be tested without a document.
/// </para>
/// </remarks>
public static class CaretNavigation
{
    /// <summary>The first cell of the document, which is where the text starts.</summary>
    /// <param name="tree">The tree to search.</param>
    /// <exception cref="EditingException">The tree has no cell the caret could stand in.</exception>
    public static CaretPosition First(EditorTree tree)
    {
        var places = CaretPlaces.Of(tree);

        return places.Count > 0
            ? CaretPosition.At(places[0], 0)
            : throw new EditingException("The document has no cell the caret could be placed in.");
    }

    /// <summary>The last cell of the document, with the caret after its text.</summary>
    /// <param name="tree">The tree to search.</param>
    /// <exception cref="EditingException">The tree has no cell the caret could stand in.</exception>
    public static CaretPosition Last(EditorTree tree)
    {
        var places = CaretPlaces.Of(tree);

        return places.Count > 0
            ? CaretPosition.At(places[^1], places[^1].Length)
            : throw new EditingException("The document has no cell the caret could be placed in.");
    }

    /// <summary>
    /// Coerces <paramref name="position"/> into <paramref name="tree"/>: the same cell with the offset
    /// clamped to its text, a cell that still exists after a rebuild, or the closest sensible cell
    /// when the one the position names is gone.
    /// </summary>
    /// <param name="tree">The tree the position has to be valid in.</param>
    /// <param name="position">The position to coerce.</param>
    /// <remarks>
    /// A position can name a cell that is no longer there in two ways: the address belonged to a node
    /// or a container that the rebuild removed, or it belonged to a cell that the notation no longer
    /// reaches. Both cases fall back to the first caret target at or after the place the address had,
    /// because that is where the text the caret was next to moved to; a position that names nothing
    /// left in the document falls back to its first cell.
    /// </remarks>
    /// <exception cref="EditingException">The tree has no cell the caret could stand in.</exception>
    public static CaretPosition Nearest(EditorTree tree, CaretPosition position)
    {
        var places = CaretPlaces.Of(tree);

        if (places.Count == 0)
        {
            throw new EditingException("The document has no cell the caret could be placed in.");
        }

        var index = CaretPlaces.Resolve(places, position);

        if (index >= 0)
        {
            return CaretPosition.At(places[index], Math.Clamp(position.Offset, 0, places[index].Length));
        }

        var order = OrderOf(tree, position.Address);

        if (order < 0)
        {
            return CaretPosition.At(places[0], 0);
        }

        foreach (var place in places)
        {
            if (place.Order >= order)
            {
                return CaretPosition.At(place, 0);
            }
        }

        return CaretPosition.At(places[^1], 0);
    }

    /// <summary>
    /// The cell <paramref name="position"/> stands in, or <see langword="null"/> when the tree has no
    /// such cell.
    /// </summary>
    /// <param name="tree">The tree the position has to be valid in.</param>
    /// <param name="position">The position to look up.</param>
    /// <remarks>
    /// This is how a host finds out what the caret is on - which text it may type into, whether the cell
    /// is a slot waiting to be filled - and it is also the only honest way to tell two cells apart that
    /// share an address, since a node's keywords all belong to that node.
    /// </remarks>
    public static CaretPlace? PlaceAt(EditorTree tree, CaretPosition position)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return CaretPlaces.PlaceAt(CaretPlaces.Of(tree), position);
    }

    /// <summary>Moves the caret <paramref name="move"/> from <paramref name="position"/>.</summary>
    /// <param name="layout">The layout the positions are expressed in.</param>
    /// <param name="measure">The measure the layout was produced with.</param>
    /// <param name="position">Where the caret starts.</param>
    /// <param name="move">Which way to go.</param>
    /// <returns>
    /// The position the caret arrives at. It is a cell of <paramref name="layout"/> and its offset is
    /// within that cell's text, so the caller can use it without checking anything; a move that cannot
    /// go anywhere returns the clamped position it started from.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="move"/> is not a known move.</exception>
    /// <exception cref="EditingException">The tree has no cell the caret could stand in.</exception>
    public static CaretPosition Move(
        CellLayout layout,
        ITextMeasure measure,
        CaretPosition position,
        CaretMove move)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(measure);

        var places = CaretPlaces.Of(layout.Tree);

        if (places.Count == 0)
        {
            throw new EditingException("The document has no cell the caret could be placed in.");
        }

        var index = CaretPlaces.Resolve(places, position);

        if (index < 0)
        {
            return Nearest(layout.Tree, position);
        }

        var place = places[index];
        var offset = Math.Clamp(position.Offset, 0, place.Length);

        return move switch
        {
            CaretMove.Left => Cell(places, index, direction: -1),
            CaretMove.Right => Cell(places, index, direction: 1),
            CaretMove.LeftCharacter => Character(places, index, offset, direction: -1),
            CaretMove.RightCharacter => Character(places, index, offset, direction: 1),
            CaretMove.Up => Vertical(layout, measure, places, index, offset, direction: -1),
            CaretMove.Down => Vertical(layout, measure, places, index, offset, direction: 1),
            CaretMove.LineStart => LineEdge(layout, measure, places, index, start: true),
            CaretMove.LineEnd => LineEdge(layout, measure, places, index, start: false),
            CaretMove.DocumentStart => CaretPosition.At(places[0], 0),
            CaretMove.DocumentEnd => CaretPosition.At(places[^1], places[^1].Length),
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, "The move is not a known caret move."),
        };
    }

    /// <summary>
    /// The neighbouring cell in document order, or this one at the edge of the document.
    /// </summary>
    /// <remarks>
    /// The arrival offset is the one a reader would expect from a structural move: going right lands on
    /// the first character of the next cell, going left on the end of the previous one. That makes two
    /// presses in opposite directions return to the same place, which a move that kept the offset would
    /// not.
    /// </remarks>
    private static CaretPosition Cell(IReadOnlyList<CaretPlace> places, int index, int direction)
    {
        var place = places[index];

        if (direction < 0)
        {
            return index > 0
                ? CaretPosition.At(places[index - 1], places[index - 1].Length)
                : CaretPosition.At(place, 0);
        }

        return index < places.Count - 1
            ? CaretPosition.At(places[index + 1], 0)
            : CaretPosition.At(place, place.Length);
    }

    /// <summary>The neighbouring offset, crossing into the next or previous cell at the edge of this one.</summary>
    private static CaretPosition Character(IReadOnlyList<CaretPlace> places, int index, int offset, int direction)
    {
        var place = places[index];

        if (direction < 0)
        {
            if (offset > 0)
            {
                return CaretPosition.At(place, offset - 1);
            }

            return index > 0
                ? CaretPosition.At(places[index - 1], places[index - 1].Length)
                : CaretPosition.At(place, 0);
        }

        if (offset < place.Length)
        {
            return CaretPosition.At(place, offset + 1);
        }

        return index < places.Count - 1
            ? CaretPosition.At(places[index + 1], 0)
            : CaretPosition.At(place, place.Length);
    }

    /// <summary>The cell on the nearest line in <paramref name="direction"/> that keeps the column.</summary>
    /// <remarks>
    /// The column is compared against the whole width of a candidate cell rather than against its
    /// start, so a line that is indented further still receives the caret when the column falls inside
    /// one of its cells. Ties go to the cell that comes first in document order, which is the leftmost
    /// one on that line.
    /// </remarks>
    private static CaretPosition Vertical(
        CellLayout layout,
        ITextMeasure measure,
        IReadOnlyList<CaretPlace> places,
        int index,
        int offset,
        int direction)
    {
        var place = places[index];

        if (layout.BoundsOf(place.Cell) is not { } origin)
        {
            return CaretPosition.At(place, offset);
        }

        var column = origin.X + CaretBounds.PrefixWidth(place, offset, measure);
        var line = MiddleOf(origin);
        var target = double.NaN;
        var best = -1;
        var distance = double.MaxValue;

        for (var candidate = 0; candidate < places.Count; candidate++)
        {
            if (layout.BoundsOf(places[candidate].Cell) is not { } bounds)
            {
                continue;
            }

            var middle = MiddleOf(bounds);

            if (direction < 0 ? middle >= line : middle <= line)
            {
                continue;
            }

            var candidateDistance = DistanceTo(bounds, column);

            if (double.IsNaN(target) || (direction < 0 ? middle > target : middle < target))
            {
                target = middle;
                best = candidate;
                distance = candidateDistance;
            }
            else if (Math.Abs(middle - target) < measure.LineHeight / 2 && candidateDistance < distance)
            {
                best = candidate;
                distance = candidateDistance;
            }
        }

        return best < 0 || layout.BoundsOf(places[best].Cell) is not { } landing
            ? CaretPosition.At(place, offset)
            : CaretPosition.At(places[best], OffsetAt(measure, places[best], column - landing.X));
    }

    /// <summary>The start or the end of the line the caret is on.</summary>
    private static CaretPosition LineEdge(
        CellLayout layout,
        ITextMeasure measure,
        IReadOnlyList<CaretPlace> places,
        int index,
        bool start)
    {
        if (layout.BoundsOf(places[index].Cell) is not { } origin)
        {
            return CaretPosition.At(places[index], start ? 0 : places[index].Length);
        }

        var line = MiddleOf(origin);
        var tolerance = measure.LineHeight / 2;
        var found = -1;

        for (var candidate = 0; candidate < places.Count; candidate++)
        {
            if (layout.BoundsOf(places[candidate].Cell) is { } bounds &&
                Math.Abs(MiddleOf(bounds) - line) < tolerance)
            {
                found = candidate;

                if (start)
                {
                    break;
                }
            }
        }

        return found < 0
            ? CaretPosition.At(places[index], start ? 0 : places[index].Length)
            : CaretPosition.At(places[found], start ? 0 : places[found].Length);
    }

    /// <summary>The offset into <paramref name="place"/>'s text whose caret is closest to <paramref name="x"/>.</summary>
    private static int OffsetAt(ITextMeasure measure, CaretPlace place, double x)
    {
        var best = 0;
        var distance = Math.Abs(x);

        for (var offset = 1; offset <= place.Length; offset++)
        {
            var candidate = Math.Abs(CaretBounds.PrefixWidth(place, offset, measure) - x);

            if (candidate < distance)
            {
                best = offset;
                distance = candidate;
            }
        }

        return best;
    }

    private static double MiddleOf(LayoutRect bounds) => bounds.Y + (bounds.Height / 2);

    private static double DistanceTo(LayoutRect bounds, double x) =>
        x < bounds.X ? bounds.X - x : x > bounds.Right ? x - bounds.Right : 0;

    private static int OrderOf(EditorTree tree, CellAddress address)
    {
        var order = 0;

        foreach (var cell in tree.Cells())
        {
            if (cell.Address == address)
            {
                return order;
            }

            order++;
        }

        return -1;
    }
}
