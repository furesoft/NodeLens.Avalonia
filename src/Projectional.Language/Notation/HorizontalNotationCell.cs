namespace Projectional.Language;

/// <summary>
/// Block cells arranged side by side instead of one below the other.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="SequenceNotationCell"/> stacks and a <see cref="LineNotationCell"/> lies in one row, and the two
/// cannot be mixed: a line holds inline cells and a sequence holds blocks, so a notation that has to put a
/// <em>block</em> beside another block had nowhere to say it. That is the case this cell answers - a notation whose
/// cells are each a little diagram of their own (an editor model's <c>[</c>, its cells and its <c>]</c>) and that
/// still reads as one row.
/// </para>
/// <para>
/// The content is blocks, exactly like a sequence's, and that is deliberate: the type hierarchy still says what a
/// notation may hold, so the one thing that changed is the direction, not the kind. An author who wants a bare
/// bracket beside a block writes <c>ed.Line(ed.Punct("["))</c> - a horizontal container with one inline cell - which
/// reads as exactly what it is.
/// </para>
/// <para>
/// It is the one place where a notation decides the orientation of something inside it. The projection rule that a
/// child's vertical notation breaks the line still holds for everything inside a line; this cell is the explicit
/// exception, because here the parent says the direction instead of inheriting it, and the layout treats it as a
/// horizontal run (hanging continuation included, so a tall block beside a short one keeps the row readable).
/// </para>
/// </remarks>
public sealed class HorizontalNotationCell : BlockNotationCell
{
    public HorizontalNotationCell(params ReadOnlySpan<BlockNotationCell> content)
    {
        Content = [.. content];

        foreach (var cell in Content)
        {
            ArgumentNullException.ThrowIfNull(cell);
        }
    }

    /// <summary>The blocks arranged side by side, in drawing order.</summary>
    public IReadOnlyList<BlockNotationCell> Content { get; }

    public override string ToString() => $"side by side ({Content.Count})";
}
