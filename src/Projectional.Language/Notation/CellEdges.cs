namespace Projectional.Language;

/// <summary>
/// How much room each side of a cell takes, in layout units.
/// </summary>
/// <remarks>
/// <para>
/// Four numbers rather than one, because the sides are not interchangeable: a table's header row pads its top and
/// bottom and not its sides, and a chip pads its left and right and not its top and bottom. A single "padding" would
/// force every notation to accept the same amount on all four sides, which is the one thing a table never wants.
/// </para>
/// <para>
/// The numbers are layout units, the same ones <see cref="SpaceNotationCell"/> and the collection spacing already
/// use, so a decoration costs the same wherever it is written and a measure decides what a unit is. Negative values
/// throw: a decoration that eats into its own content is a layout bug, not a feature.
/// </para>
/// </remarks>
public readonly record struct CellEdges
{
    /// <summary>Creates edges from one number per side.</summary>
    /// <param name="top">The room above the content.</param>
    /// <param name="right">The room to the right of the content.</param>
    /// <param name="bottom">The room below the content.</param>
    /// <param name="left">The room to the left of the content.</param>
    /// <exception cref="ArgumentOutOfRangeException">A side is negative.</exception>
    public CellEdges(double top, double right, double bottom, double left)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(top);
        ArgumentOutOfRangeException.ThrowIfNegative(right);
        ArgumentOutOfRangeException.ThrowIfNegative(bottom);
        ArgumentOutOfRangeException.ThrowIfNegative(left);

        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    /// <summary>No room on any side, which is what an undecorated cell has.</summary>
    public static CellEdges Zero => default;

    /// <summary>The same amount on all four sides.</summary>
    public static CellEdges All(double value) => new(value, value, value, value);

    /// <summary>One amount for the top and bottom and another for the left and right.</summary>
    public static CellEdges Symmetric(double horizontal, double vertical) => new(vertical, horizontal, vertical, horizontal);

    /// <summary>The room above the content.</summary>
    public double Top { get; }

    /// <summary>The room to the right of the content.</summary>
    public double Right { get; }

    /// <summary>The room below the content.</summary>
    public double Bottom { get; }

    /// <summary>The room to the left of the content.</summary>
    public double Left { get; }

    /// <summary>What the left and right sides add up to, which is what a width grows by.</summary>
    public double Horizontal => Left + Right;

    /// <summary>What the top and bottom sides add up to, which is what a height grows by.</summary>
    public double Vertical => Top + Bottom;

    /// <summary>Whether every side is zero.</summary>
    public bool IsZero => Top == 0 && Right == 0 && Bottom == 0 && Left == 0;

    /// <summary>The sum of two edges, side by side.</summary>
    public CellEdges Plus(CellEdges other) => new(Top + other.Top, Right + other.Right, Bottom + other.Bottom, Left + other.Left);

    public override string ToString() => $"edges {Top}/{Right}/{Bottom}/{Left}";
}
