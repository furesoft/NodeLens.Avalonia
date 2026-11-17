using Projectional.Projection;

namespace Projectional.Workbench;

/// <summary>One row of the output panel: a line of a run, and the node it came from.</summary>
/// <param name="Text">The line as it is drawn - indented by the entry's depth, already spelled out.</param>
/// <param name="Place">The cell the row jumps to, or <see langword="null"/> for a line about no node.</param>
/// <remarks>
/// The row carries a <see cref="CellAddress"/> for the same reason a problem does: a trace answers "why", and the
/// answer is only useful if the user can go and look at the node that produced the line.
/// </remarks>
internal sealed record RunRow(string Text, CellAddress? Place)
{
    /// <summary>The row as one line, which is what the panel's list draws.</summary>
    public override string ToString() => Text;
}
