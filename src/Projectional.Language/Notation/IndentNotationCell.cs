namespace Projectional.Language;

/// <summary>
/// A block nested one indentation level deeper than the line that owns it.
/// </summary>
/// <remarks>
/// Indentation is structure, not characters: the engine decides how wide a level is, so changing the
/// indent width is a theme change and never touches a notation or the model.
/// </remarks>
public sealed class IndentNotationCell : BlockNotationCell
{
    public IndentNotationCell(params ReadOnlySpan<BlockNotationCell> content)
    {
        Content = [.. content];
    }

    public IReadOnlyList<BlockNotationCell> Content { get; }

    public override string ToString() => $"indent({Content.Count})";
}
