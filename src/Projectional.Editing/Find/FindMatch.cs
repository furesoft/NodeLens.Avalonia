using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>One occurrence of a query in the document: where it is and how long it is.</summary>
/// <param name="Address">The cell the occurrence is in.</param>
/// <param name="Offset">Where in that cell's text it starts.</param>
/// <param name="Length">How many characters it covers.</param>
/// <remarks>
/// The address is a cell rather than a node because a search finds text, and text belongs to one cell. That is also
/// what makes a match directly actionable: the range it names is the selection that replaces it.
/// </remarks>
public readonly record struct FindMatch(CellAddress Address, int Offset, int Length)
{
    /// <summary>One past the last character of the occurrence.</summary>
    public int End => Offset + Length;

    public override string ToString() => $"{Address}:{Offset}+{Length}";
}