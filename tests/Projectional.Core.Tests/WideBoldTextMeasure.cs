using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Core.Tests;

/// <summary>
/// A measure that charges a bold character twice the width of a regular one.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MonospaceTextMeasure"/> deliberately ignores emphasis, which makes it useless for asking
/// whether the decoration reached the geometry. A proportional face really does widen when it is set
/// bold, so this measure exaggerates that: two units per bold character instead of one, which keeps
/// the expected numbers countable and impossible to satisfy by accident.
/// </para>
/// <para>
/// Only bold widens. Italic, underline and strikethrough keep the advance, which is what the real
/// measure does as well and what a test that expects a struck-through cell to stay put relies on.
/// </para>
/// </remarks>
internal sealed class WideBoldTextMeasure : ITextMeasure
{
    private WideBoldTextMeasure()
    {
    }

    public static WideBoldTextMeasure Instance { get; } = new();

    public double Measure(string text, TextEmphasis emphasis)
    {
        ArgumentNullException.ThrowIfNull(text);

        var columns = (emphasis & TextEmphasis.Bold) != 0 ? 2 : 1;

        return text.Length * columns;
    }

    public double LineHeight => 1;

    public double IndentWidth => 2;

    public override string ToString() => "double-bold";
}
