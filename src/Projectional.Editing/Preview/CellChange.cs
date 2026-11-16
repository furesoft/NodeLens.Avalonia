using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>One mark on the ghost: this cell is drawn as added, changed or removed.</summary>
/// <remarks>
/// <para>
/// The mark names a <see cref="CellAddress"/> rather than a cell, for the same reason everything above the tree
/// does: the ghost is rebuilt when the preview is recomputed, and a cell object does not survive that. A host
/// resolves the address against the tree it is about to draw.
/// </para>
/// <para>
/// <see cref="OldValue"/> is only meaningful for a <see cref="CellChangeKind.Changed"/> or
/// <see cref="CellChangeKind.Removed"/> mark, where the ghost shows the new text and the old one is worth naming.
/// It is <see langword="null"/> for an addition, and for a property that was unset rather than rewritten - an
/// unset value is not the empty string.
/// </para>
/// </remarks>
/// <param name="Address">The cell the mark is on.</param>
/// <param name="Kind">What happened to it.</param>
/// <param name="OldValue">The earlier value or name, when there is one to show.</param>
public readonly record struct CellChange(CellAddress Address, CellChangeKind Kind, string? OldValue = null)
{
    public override string ToString() =>
        OldValue is null ? $"{Kind} {Address}" : $"{Kind} {Address} (was '{OldValue}')";
}
