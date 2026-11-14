namespace Projectional.Language;

/// <summary>
/// Several blocks stacked vertically, unchanged. This is how a notation states "these lines, then
/// those" without pretending the extra level is an indentation.
/// </summary>
/// <remarks>
/// Indentation is what <see cref="IndentNotationCell"/> is for; the two are deliberately separate so
/// that a notation says exactly one thing at a time. A sequence is usually the root of a notation,
/// or a group of lines inside an indent.
/// </remarks>
public sealed class SequenceNotationCell : BlockNotationCell
{
    public SequenceNotationCell(params ReadOnlySpan<BlockNotationCell> content)
    {
        Content = [.. content];
    }

    public IReadOnlyList<BlockNotationCell> Content { get; }

    public override string ToString() => $"sequence({Content.Count})";
}
