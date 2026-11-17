namespace Projectional.Language;

/// <summary>Literal text a template emits, exactly as written.</summary>
/// <remarks>
/// A literal is the only part whose output does not depend on the node, which is what makes keywords, punctuation
/// and the shape of a statement expressible at all. Indentation is <em>not</em> part of a literal: a template that
/// wrote its own spaces would disagree with the indent level around it the moment it is nested.
/// </remarks>
public sealed record TextPart : TemplatePart
{
    /// <summary>Creates a part that emits <paramref name="text"/>.</summary>
    /// <param name="text">The text to emit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public TextPart(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
    }

    /// <summary>The text to emit.</summary>
    public string Text { get; }

    public override string ToString() => $"text {Text}";
}
