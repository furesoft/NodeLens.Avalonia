namespace Projectional.Language;

/// <summary>A template emitted one indentation level deeper.</summary>
/// <remarks>
/// Indentation is a property of the template, never of the text that was emitted before it: the part raises the
/// level for every line break inside its body and lowers it again afterwards, so a nested block cannot be indented
/// by whatever its child happened to produce. The level is measured in the generator's indent width, which is the
/// one thing about layout a generator has to know.
/// </remarks>
public sealed record IndentPart : TemplatePart
{
    /// <summary>Creates a part that emits <paramref name="body"/> one level deeper.</summary>
    /// <param name="body">What to emit, indented.</param>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is <see langword="null"/>.</exception>
    public IndentPart(TextTemplate body)
    {
        ArgumentNullException.ThrowIfNull(body);

        Body = body;
    }

    /// <summary>What to emit, indented.</summary>
    public TextTemplate Body { get; }

    public override string ToString() => "indent";
}
