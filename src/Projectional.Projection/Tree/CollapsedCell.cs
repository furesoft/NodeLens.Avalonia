using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Stands in for a folded node: one line that says what is inside instead of drawing it.
/// </summary>
/// <remarks>
/// <para>
/// The cell carries the node's <em>own</em> address, not a synthetic one, and that single choice is why the
/// rest of the editor needs no knowledge of folding: a press inside it resolves to the node, so delete and
/// move act on the node and its whole subtree, and the caret reads the address as "start here" when the
/// node is unfolded. Typing is refused because the cell is not a <see cref="PropertyCell"/>, which is the
/// point of folding it.
/// </para>
/// <para>
/// It derives from <see cref="TextCell"/> deliberately: layout arranges it with the text branch it already
/// has, the snapshot renderer draws it, the caret walk offers it as one place and the hit test finds it -
/// none of those files change.
/// </para>
/// </remarks>
public sealed class CollapsedCell : TextCell
{
    /// <summary>Creates the stand-in for a folded node.</summary>
    /// <param name="address">The node's own address.</param>
    /// <param name="summary">What the line says, as <see cref="CollapsedSummary.Of"/> answered it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="summary"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="summary"/> is empty.</exception>
    public CollapsedCell(CellAddress address, string summary)
        : base(address, summary, TextEmphasis.None)
    {
        ArgumentException.ThrowIfNullOrEmpty(summary);

        Summary = summary;
    }

    /// <summary>What the line says, kept because a host may want to read it back.</summary>
    public string Summary { get; }

    internal override string Describe() => $"collapsed to '{Summary}'";
}
