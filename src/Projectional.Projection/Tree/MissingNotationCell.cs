namespace Projectional.Projection;

/// <summary>
/// Stands in for a node whose concept has no notation registered.
/// </summary>
/// <remarks>
/// This is a marker, not an error. A language under construction has concepts without notation, and
/// a model that uses one must still open, save and show the rest of itself; throwing here would turn
/// a missing style into a model the editor cannot display at all. The cell shows the concept key so
/// the gap is diagnosable from the editor itself, and the node stays reachable through its address so
/// that a notation added later needs no model change.
/// </remarks>
public sealed class MissingNotationCell : TextCell
{
    public MissingNotationCell(CellAddress address, string conceptKey)
        : base(address, $"<no notation for {conceptKey}>")
    {
        ArgumentException.ThrowIfNullOrEmpty(conceptKey);
        ConceptKey = conceptKey;
    }

    /// <summary>The concept that should have had a notation.</summary>
    public string ConceptKey { get; }

    internal override string Describe() => $"no notation for {ConceptKey}";
}
