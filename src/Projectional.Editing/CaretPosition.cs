using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Where the caret stands: in which cell, and how far into that cell's text.
/// </summary>
/// <remarks>
/// <para>
/// An address cannot name a cell on its own. Every constant and every space of a node carries that
/// node's address, because a notation's keywords and punctuation are not separate model entities -
/// they belong to the node that shows them. Two cells of one line therefore share an address, and
/// "the address plus an offset" could not tell the opening brace at the end of a line from the
/// keyword at its start. A position consequently also remembers where its cell sits in document
/// order, which is what makes <see cref="With"/> and the moves within one tree exact.
/// </para>
/// <para>
/// That order is a hint and not an identity. A position named by address alone carries none, and
/// <see cref="CaretNavigation.Nearest"/> then falls back to the first cell with that address - which
/// is precisely what an edit action means when it reports "the caret ends up at this address" - and
/// to the first slot inside it when that cell arranges others, because a slot is what can be typed
/// into. After a rebuild the order of a surviving cell is at worst slightly off, and the nearest cell
/// with the same address is still the one that was meant.
/// </para>
/// <para>
/// The offset counts characters, not pixels, which keeps positions independent of fonts and zoom
/// levels: a change of measure never invalidates a caret. An offset is only checked against the
/// text once a cell is involved; a position on a cell that has since become shorter is clamped when
/// it is used rather than when it is created, because that is the only moment the real text is
/// known.
/// </para>
/// </remarks>
public readonly record struct CaretPosition
{
    /// <summary>Creates a position in the cell at <paramref name="address"/>.</summary>
    /// <param name="address">The address of a cell, which must be set.</param>
    /// <param name="offset">How many characters of the cell's text are to the left of the caret.</param>
    /// <exception cref="ArgumentException"><paramref name="address"/> is the default address.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    public CaretPosition(CellAddress address, int offset = 0)
        : this(address, offset, order: -1)
    {
    }

    private CaretPosition(CellAddress address, int offset, int order)
    {
        if (address.Owner.IsEmpty)
        {
            throw new ArgumentException(
                "A caret position needs the address of a cell, but the default address was given.",
                nameof(address));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        Address = address;
        Offset = offset;
        Order = order;
    }

    /// <summary>The address of the cell the caret stands in.</summary>
    public CellAddress Address { get; }

    /// <summary>How many characters of the cell's text are to the left of the caret.</summary>
    public int Offset { get; }

    /// <summary>
    /// Where the cell stood in document order when this position was created, or <c>-1</c> when the
    /// position was named by address alone.
    /// </summary>
    internal int Order { get; }

    /// <summary>A position at <paramref name="offset"/> in the cell at <paramref name="address"/>.</summary>
    /// <param name="address">The address of a cell, which must be set.</param>
    /// <param name="offset">How many characters of the cell's text are to the left of the caret.</param>
    public static CaretPosition At(CellAddress address, int offset = 0) => new(address, offset);

    /// <summary>A position at <paramref name="offset"/> in <paramref name="place"/>.</summary>
    internal static CaretPosition At(CaretPlace place, int offset) => new(place.Address, offset, place.Order);

    /// <summary>The same cell, with the caret <paramref name="offset"/> characters into its text.</summary>
    /// <param name="offset">How many characters of the cell's text are to the left of the caret.</param>
    public CaretPosition With(int offset) => new(Address, offset, Order);

    /// <inheritdoc />
    public override string ToString() => $"{Address}:{Offset}";
}
