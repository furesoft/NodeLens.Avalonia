namespace Projectional.Language;

/// <summary>Literal text in a notation: a keyword or a piece of punctuation.</summary>
/// <remarks>
/// A constant is not editable text. It participates in substitution menus — replacing a keyword is
/// how a transformation is offered — but typing never changes it.
/// </remarks>
public sealed class ConstantNotationCell : InlineNotationCell
{
    public ConstantNotationCell(string text, ConstantKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Text = text;
        Kind = kind;
    }

    public string Text { get; }

    public ConstantKind Kind { get; }

    public override string ToString() => $"'{Text}'";
}
