using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// A caret plus the end of the selection that is being held, which is the same place when nothing is
/// selected.
/// </summary>
/// <remarks>
/// <para>
/// The anchor is stored rather than normalised away. Extending a selection moves the caret and keeps
/// the anchor, so a selection is a pair and collapsing it to a sorted range would lose which end the
/// user is dragging; <see cref="Start"/> and <see cref="End"/> are derived for the readers that want
/// the range. Select All and a mouse drag both produce a non-empty value today; shift-arrow is still
/// missing.
/// </para>
/// <para>
/// A selection may span several cells, and that is not a degraded state - it is how a node is selected.
/// Select All widens from one property's text to everything the node under the caret shows, so the
/// range that names a node is the run of caret places from its first cell to its last. What a
/// multi-cell selection cannot do is hand text to the text actions: <see cref="HasText"/> is false and
/// they fall back to caret behaviour. Structural actions read the range itself.
/// </para>
/// </remarks>
public readonly record struct Selection
{
    /// <summary>Creates a selection from its two ends.</summary>
    public Selection(CaretPosition anchor, CaretPosition caret)
    {
        Anchor = anchor;
        Caret = caret;
    }

    /// <summary>The end the selection was started from.</summary>
    public CaretPosition Anchor { get; }

    /// <summary>The end the caret is at.</summary>
    public CaretPosition Caret { get; }

    /// <summary>Nothing selected: an anchor and a caret in the same place.</summary>
    public static Selection At(CaretPosition caret) => new(caret, caret);

    /// <summary>Nothing selected, at the start of the cell at <paramref name="address"/>.</summary>
    public static Selection At(CellAddress address) => At(CaretPosition.At(address));

    /// <summary>Whether both ends are the same position.</summary>
    public bool IsEmpty => Anchor.Equals(Caret);

    /// <summary>Whether both ends are inside one cell, which is what makes the range meaningful.</summary>
    public bool IsSingleCell => Anchor.Address.Equals(Caret.Address);

    /// <summary>Whether something is selected inside one cell.</summary>
    public bool HasText => IsSingleCell && !IsEmpty;

    /// <summary>The smaller offset of the two ends; only meaningful for a single-cell selection.</summary>
    public int Start => Math.Min(Anchor.Offset, Caret.Offset);

    /// <summary>The larger offset of the two ends; only meaningful for a single-cell selection.</summary>
    public int End => Math.Max(Anchor.Offset, Caret.Offset);

    public override string ToString() => IsEmpty ? Caret.ToString() : $"{Anchor}..{Caret}";
}
