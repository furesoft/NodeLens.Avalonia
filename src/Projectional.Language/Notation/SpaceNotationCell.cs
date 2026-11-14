namespace Projectional.Language;

/// <summary>One space between two inline cells.</summary>
/// <remarks>
/// Spaces are explicit because there are no whitespace semantics anywhere in the editor: a space is
/// a cell the user can stand on, which is what makes "the caret is always somewhere visible" true.
/// </remarks>
public sealed class SpaceNotationCell : InlineNotationCell
{
    private SpaceNotationCell()
    {
    }

    /// <summary>The single instance: spaces carry no data, so they need no identity either.</summary>
    public static SpaceNotationCell Instance { get; } = new();

    public override string ToString() => "space";
}
