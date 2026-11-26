namespace Projectional.Language;

/// <summary>
/// A line drawn around a cell, in layout units, in the role the palette resolves.
/// </summary>
/// <remarks>
/// A border is one thickness all round rather than a set of four, because a notation that draws a partial border is
/// drawing a table's grid, not a box - and a grid is what a <see cref="CellPaint"/> on each cell of a table produces.
/// The thickness is measured in layout units like every other distance in this layer, so a border is a real part of
/// the cell's size and the caret and hit-testing account for it without a second rule.
/// </remarks>
public readonly record struct CellBorder
{
    /// <summary>Creates a border of one thickness in one role.</summary>
    /// <param name="thickness">The line's width in layout units.</param>
    /// <param name="role">What the line means, which the palette turns into a colour.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="thickness"/> is negative.</exception>
    public CellBorder(double thickness, PaintRole role = PaintRole.Neutral)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(thickness);

        Thickness = thickness;
        Role = role;
    }

    /// <summary>No border, which is what an undecorated cell has.</summary>
    public static CellBorder None => default;

    /// <summary>The line's width in layout units; zero draws nothing.</summary>
    public double Thickness { get; }

    /// <summary>What the line means, which the palette turns into a colour.</summary>
    public PaintRole Role { get; }

    /// <summary>Whether the border is thick enough to draw.</summary>
    public bool IsVisible => Thickness > 0;

    public override string ToString() => IsVisible ? $"border {Thickness} {Role}" : "no border";
}
