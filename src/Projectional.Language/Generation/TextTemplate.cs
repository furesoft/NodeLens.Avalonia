namespace Projectional.Language;

/// <summary>What a generator emits for one concept: an ordered list of parts.</summary>
/// <remarks>
/// An empty template is legal and means "this concept emits nothing" - a grouping node that only exists in the model,
/// or a marker whose text lives on its children. That is different from a concept with no template at all, which is a
/// failure: nobody said what to emit, so the generator stops instead of silently leaving a hole in the output.
/// </remarks>
public sealed class TextTemplate
{
    /// <summary>The template that emits nothing.</summary>
    public static TextTemplate Empty { get; } = new([]);

    /// <summary>Creates a template from <paramref name="parts"/>.</summary>
    /// <param name="parts">The parts, in the order they emit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> is <see langword="null"/>.</exception>
    public TextTemplate(IReadOnlyList<TemplatePart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        Parts = parts;
    }

    /// <summary>The parts, in the order they emit.</summary>
    public IReadOnlyList<TemplatePart> Parts { get; }

    /// <summary>Whether this template emits nothing at all.</summary>
    public bool IsEmpty => Parts.Count == 0;

    /// <summary>Creates a template from a handful of parts.</summary>
    /// <param name="parts">The parts, in the order they emit.</param>
    /// <remarks>
    /// <c>params ReadOnlySpan</c> so a generator written in C# passes literals without allocating an array - the same
    /// reason <see cref="PropertyType.Enum"/> takes its symbols that way.
    /// </remarks>
    public static TextTemplate Of(params ReadOnlySpan<TemplatePart> parts) => new([.. parts]);

    public override string ToString() => $"{Parts.Count} part(s)";
}
