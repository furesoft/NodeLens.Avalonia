namespace Projectional.Language;

/// <summary>
/// A child slot drawn as a separated list: the children's lines, with a separator drawn between the
/// last line of one child and the first line of the next.
/// </summary>
/// <remarks>
/// The separator is a cell rather than a character because a list of comma-separated expressions and
/// a list of blocks need the same behaviour with a different visual: one draws <c>,</c>, the other
/// draws nothing at all.
/// </remarks>
public sealed class CollectionNotationCell : BlockNotationCell
{
    public CollectionNotationCell(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    public string Slot { get; }

    /// <summary>Drawn between two children, or <see langword="null"/> for none.</summary>
    public InlineNotationCell? Separator { get; init; }

    public string EmptyText { get; init; } = "<empty>";

    public override string ToString() => $"collection {Slot}";
}
