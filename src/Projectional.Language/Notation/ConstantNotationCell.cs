namespace Projectional.Language;

/// <summary>Literal text in a notation: a keyword or a piece of punctuation.</summary>
/// <remarks>
/// A constant is not editable text. It participates in substitution menus — replacing a keyword is
/// how a transformation is offered — but typing never changes it.
/// </remarks>
public sealed class ConstantNotationCell : InlineNotationCell
{
    public ConstantNotationCell(string text, ConstantKind kind, TextEmphasis emphasis = TextEmphasis.None)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Text = text;
        Kind = kind;
        Emphasis = emphasis;
    }

    public string Text { get; }

    public ConstantKind Kind { get; }

    /// <summary>
    /// How this constant is decorated wherever it appears. A language uses it to make a keyword carry
    /// weight, and it applies to every place the notation is used, which is why it belongs to the
    /// notation rather than to a model.
    /// </summary>
    public TextEmphasis Emphasis { get; }

    public override string ToString() => $"'{Text}'";
}
