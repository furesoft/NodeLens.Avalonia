using Projectional.Language;

namespace Projectional.Projection;

/// <summary>
/// Text the notation wrote down: a keyword, punctuation, or whatever stands in for a node that has
/// no notation of its own.
/// </summary>
/// <remarks>
/// The text is not editable and holds no value. It is here so that the line has something to show
/// where the language says words go, which is also what makes the projection read like source
/// instead of like a form.
/// </remarks>
public sealed class ConstantCell : TextCell
{
    public ConstantCell(
        CellAddress address,
        string text,
        ConstantKind kind,
        TextEmphasis emphasis = TextEmphasis.None)
        : base(address, text, emphasis)
    {
        Kind = kind;
    }

    /// <summary>What the text is made of, which is what styles key off.</summary>
    public ConstantKind Kind { get; }

    /// <summary>
    /// Whether the caret stops in this text. An annotation is drawn but is not a caret stop: it is an inlay hint the
    /// projection computed, not text the author wrote, so stepping into it would leave the caret in something the
    /// model does not hold. A keyword or a punctuation mark is a stop as before.
    /// </summary>
    public override bool IsCaretTarget => Kind != ConstantKind.Annotation;

    internal override string Describe() => $"'{Text}'";
}
