namespace Projectional.Language;

/// <summary>
/// A child slot drawn as a separated list: the children's lines, with a separator drawn between the
/// last line of one child and the first line of the next.
/// </summary>
/// <remarks>
/// <para>
/// The separator is a cell rather than a character because a list of comma-separated expressions and
/// a list of blocks need the same behaviour with a different visual: one draws <c>,</c>, the other
/// draws nothing at all.
/// </para>
/// <para>
/// The separator does not carry the blank that makes a list readable, because a separator is often
/// punctuation that reads wrong against the words beside it - <c>a;b</c> versus <c>a; b</c>. The
/// blank is <see cref="Spacing"/> instead, so that a list of one kind of child can be as tight or as
/// airy as the language wants without every author writing the same space into the notation.
/// </para>
/// </remarks>
public sealed class CollectionNotationCell : BlockNotationCell
{
    private int _spacing = 1;

    public CollectionNotationCell(string slot)
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
    /// <remarks>
    /// The blank follows the separator rather than straddling it, because whether a token wants a space
    /// in front of it belongs to the token: a comma does not, the word <c>and</c> does. That space is
    /// written into the separator notation, where it stays next to the token it belongs to. The default
    /// of one gives <c>a; b</c> and <c>a b</c>, which is what a reader expects of a list.
    /// </remarks>
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

    public override string ToString() => $"collection {Slot}";
}
