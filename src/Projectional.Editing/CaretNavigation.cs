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
    /// </para>
    /// <para>
    /// An address that names a cell which arranges others - a node, a container, the element of a child
    /// slot - is read as "start here", and the first thing inside it that can be typed into wins over
    /// the first thing it shows. That is the same rule an empty document opens with, and it is what
    /// makes an edit that creates a node usable: the action reports the address of what it created,
    /// and the caret has to end up on the property the user is going to type, not on the keyword the
    /// notation happens to render in front of it.
    /// </para>
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

        return CaretPosition.At(places[Landing(tree, places, position.Address, order)], 0);
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
    /// <param name="page">
    /// How tall the visible area is, in layout units, for the page moves; ignored by every other move.
    /// </param>
    /// <returns>
    /// The position the caret arrives at. It is a cell of <paramref name="layout"/> and its offset is
    /// within that cell's text, so the caller can use it without checking anything; a move that cannot
    /// go anywhere returns the clamped position it started from.
    /// </returns>
    /// <remarks>
    /// <paramref name="page"/> has no default on purpose. A layout does not know how large the window
    /// that shows it is - that is the host's fact, not the tree's - so a caller that wants a page move
    /// has to say how much a page is; a default would turn PageDown into Down silently. Every other
    /// move ignores the number, and <see cref="PageStep"/> answers zero for one that is unusable, which
    /// is what a host without a viewport should pass.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="move"/> is not a known move.</exception>
    /// <exception cref="EditingException">The tree has no cell the caret could stand in.</exception>
    public static CaretPosition Move(
        CellLayout layout,
        ITextMeasure measure,
        CaretPosition position,
        CaretMove move,
        double page)
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
            CaretMove.Up => Vertical(layout, measure, places, index, offset, direction: -1, distance: 0),
            CaretMove.Down => Vertical(layout, measure, places, index, offset, direction: 1, distance: 0),
            CaretMove.PageUp => Vertical(layout, measure, places, index, offset, -1, PageStep(page, measure)),
            CaretMove.PageDown => Vertical(layout, measure, places, index, offset, 1, PageStep(page, measure)),
            CaretMove.LineStart => LineEdge(layout, measure, places, index, offset, start: true),
            CaretMove.LineEnd => LineEdge(layout, measure, places, index, offset, start: false),
            CaretMove.DocumentStart => CaretPosition.At(places[0], 0),
            CaretMove.DocumentEnd => CaretPosition.At(places[^1], places[^1].Length),
            _ => throw new ArgumentOutOfRangeException(nameof(move), move, "The move is not a known caret move."),
        };
    }

    /// <summary>
    /// How far a page move travels: a whole visible page less one line, so the line the caret leaves
    /// stays on screen at the other edge.
    /// </summary>
    /// <remarks>
    /// Zero when <paramref name="page"/> is not a usable height - a host that has no viewport, or a
    /// height that is not a positive number - because a page move that cannot measure a page has to
    /// degrade into the single-line move it is a version of rather than jump nowhere.
    /// </remarks>
    private static double PageStep(double page, ITextMeasure measure) =>
        page > 0 ? Math.Max(0, page - measure.LineHeight) : 0;

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

    /// <summary>
    /// The line nearest in <paramref name="direction"/>, <paramref name="distance"/> away, that keeps the
    /// column as well as it can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lines, not cells: a wrapped value is several lines of one cell, and pressing down in the middle of
    /// it has to move one line down rather than jumping over the whole cell. The column is compared
    /// against the whole width of a candidate cell rather than against its start, so a line that is
    /// indented further still receives the caret when the column falls inside one of its cells. Ties go
    /// to the place that comes first in document order, which is the leftmost one on that line.
    /// </para>
    /// <para>
    /// <paramref name="distance"/> is what turns the single-line move into a page move: the caret aims at
    /// a line that far away instead of the one next door, and lands on the nearest line in the direction
    /// it can actually reach. The two are one routine because a page move is a vertical move that wants
    /// to land somewhere else - a document too short for a page ends up on its last line, which is what
    /// the nearest-in-direction rule already does when the aim is past the end.
    /// </para>
    /// </remarks>
    private static CaretPosition Vertical(
        CellLayout layout,
        ITextMeasure measure,
        IReadOnlyList<CaretPlace> places,
        int index,
        int offset,
        int direction,
        double distance)
    {
        var place = places[index];

        if (layout.BoundsOf(place.Cell) is not { } origin)
        {
            return CaretPosition.At(place, offset);
        }

        var lines = CaretGeometry.LinesOf(place, layout, measure);
        var lineIndex = CaretGeometry.LineIndexOf(lines, offset);
        var column = origin.X + CaretGeometry.LeftOn(place, lines, lineIndex, offset, measure);
        var line = CaretGeometry.TopOf(origin, lineIndex, measure) + (measure.LineHeight / 2);
        var aim = line + (direction * distance);
        var tolerance = measure.LineHeight / 2;
        var best = -1;
        var bestLine = 0;
        var miss = double.MaxValue;
        var distanceToColumn = double.MaxValue;

        for (var candidate = 0; candidate < places.Count; candidate++)
        {
            if (layout.BoundsOf(places[candidate].Cell) is not { } bounds)
            {
                continue;
            }

            var candidateLines = CaretGeometry.LinesOf(places[candidate], layout, measure);

            for (var row = 0; row < candidateLines.LineCount; row++)
            {
                var middle = CaretGeometry.TopOf(bounds, row, measure) + (measure.LineHeight / 2);

                if (direction < 0 ? middle >= line : middle <= line)
                {
                    continue;
                }

                var candidateMiss = Math.Abs(middle - aim);
                var candidateDistance = DistanceTo(bounds, column);

                if (best < 0
                    || candidateMiss < miss - tolerance
                    || (Math.Abs(candidateMiss - miss) <= tolerance && candidateDistance < distanceToColumn))
                {
                    best = candidate;
                    bestLine = row;
                    miss = candidateMiss;
                    distanceToColumn = candidateDistance;
                }
            }
        }

        if (best < 0 || layout.BoundsOf(places[best].Cell) is not { } landing)
        {
            return CaretPosition.At(place, offset);
        }

        var landingLines = CaretGeometry.LinesOf(places[best], layout, measure);

        return CaretPosition.At(
            places[best],
            CaretGeometry.OffsetAt(places[best], landingLines, bestLine, column - landing.X, measure));
    }

    /// <summary>The start or the end of the line the caret is on.</summary>
    /// <remarks>
    /// The edge of a line, not of the cell: at the end of the second line of a wrapped value the caret
    /// stays in the value rather than jumping over the rest of it.
    /// </remarks>
    private static CaretPosition LineEdge(
        CellLayout layout,
        ITextMeasure measure,
        IReadOnlyList<CaretPlace> places,
        int index,
        int offset,
        bool start)
    {
        var place = places[index];

        if (layout.BoundsOf(place.Cell) is not { } origin)
        {
            return CaretPosition.At(place, start ? 0 : place.Length);
        }

        var lines = CaretGeometry.LinesOf(place, layout, measure);
        var top = CaretGeometry.TopOf(origin, CaretGeometry.LineIndexOf(lines, offset), measure);
        var tolerance = measure.LineHeight / 2;
        var found = -1;
        var foundLine = 0;

        for (var candidate = 0; candidate < places.Count; candidate++)
        {
            if (layout.BoundsOf(places[candidate].Cell) is not { } bounds)
            {
                continue;
            }

            var candidateLines = CaretGeometry.LinesOf(places[candidate], layout, measure);
            var row = CaretGeometry.RowAt(candidateLines, bounds, top, tolerance, measure);

            if (row < 0)
            {
                continue;
            }

            found = candidate;
            foundLine = row;

            if (start)
            {
                break;
            }
        }

        if (found < 0)
        {
            return CaretPosition.At(place, start ? 0 : place.Length);
        }

        var foundLines = CaretGeometry.LinesOf(places[found], layout, measure);
        var line = foundLines.Lines[foundLine];

        return CaretPosition.At(places[found], start ? line.Start : line.ContentEnd);
    }

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

    /// <summary>
    /// The place a position lands on when its address names a cell that arranges others rather than a
    /// cell the caret could stand in.
    /// </summary>
    /// <param name="tree">The tree the position has to be valid in.</param>
    /// <param name="places">The places of <paramref name="tree"/>.</param>
    /// <param name="address">The address that does not name a place of its own.</param>
    /// <param name="order">Where that address sits in document order.</param>
    /// <remarks>
    /// Everything a cell arranges is one uninterrupted run of document order, so the subtree the
    /// address names is a range of orders and needs no second walk. Inside that range the first slot
    /// wins, because a slot is what can be typed into; a subtree that offers none falls back to its
    /// first place, which is the keyword or bracket the notation puts in front.
    /// </remarks>
    private static int Landing(EditorTree tree, IReadOnlyList<CaretPlace> places, CellAddress address, int order)
    {
        var first = FirstAtOrAfter(places, order);

        if (first < 0)
        {
            return places.Count - 1;
        }

        var end = tree.Find(address) is { } subtree ? order + CountOf(subtree) : order;

        for (var index = first; index < places.Count && places[index].Order < end; index++)
        {
            if (places[index].IsSlot)
            {
                return index;
            }
        }

        return first;
    }

    private static int FirstAtOrAfter(IReadOnlyList<CaretPlace> places, int order)
    {
        for (var index = 0; index < places.Count; index++)
        {
            if (places[index].Order >= order)
            {
                return index;
            }
        }

        return -1;
    }

    private static int CountOf(EditorCell cell)
    {
        var count = 1;

        foreach (var child in cell.Children)
        {
            count += CountOf(child);
        }

        return count;
    }
}
