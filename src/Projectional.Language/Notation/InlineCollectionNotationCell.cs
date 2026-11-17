namespace Projectional.Language;

/// <summary>
/// A child slot drawn as a separated list inside a line: the children's cells side by side, with a
/// separator between two neighbours.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CollectionNotationCell"/> is the same idea as a block: it starts its own line. This one is
/// the inline half, and it exists because a list can belong inside a line - a record's fields, a call's
/// arguments, a tuple. Without it the declared notation language could not express what the runtime
/// already draws (a <c>CollectionCell</c> arranges its items inline either way), so a language author
/// would have to put every list on a line of its own.
/// </para>
/// <para>
/// The two cells are separate types rather than one with a flag, for the same reason every other cell is:
/// a notation that says "inline" in the wrong place has to fail to compile, not to lay out wrongly. The
/// block/inline split is the type hierarchy and it stays there.
/// </para>
/// <para>
/// Spacing follows the separator rather than straddling it, and the blank a token wants in front of it
/// belongs to the token - exactly as in <see cref="CollectionNotationCell"/>, whose remarks say why.
/// </para>
/// </remarks>
public sealed class InlineCollectionNotationCell : InlineNotationCell
{
    private int _spacing = 1;

    public InlineCollectionNotationCell(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        Slot = slot;
    }

    public string Slot { get; }

    /// <summary>Drawn between two children, or <see langword="null"/> for none.</summary>
    public InlineNotationCell? Separator { get; init; }

    /// <summary>
    /// How many spaces follow the separator, or stand between two neighbouring children when there is
    /// no separator.
    /// </summary>
    public int Spacing
    {
        get => _spacing;
        init => _spacing = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The spacing of a collection cannot be negative.");
    }

    public string EmptyText { get; init; } = "<empty>";

    public override string ToString() => $"inline collection {Slot}";
}
