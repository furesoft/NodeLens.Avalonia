using System.Globalization;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Core.Tests;

/// <summary>
/// Monospace columns, scaled by a whole number - the geometry a font size change produces.
/// </summary>
/// <remarks>
/// <para>
/// The projection has no notion of points: a bigger font reaches it as "the same character is worth
/// more units". This measure says exactly that and nothing else, so a test that changes the measure
/// can predict every number by multiplying, and a session that ignored the new measure cannot pass by
/// accident.
/// </para>
/// <para>
/// The factor is an integer on purpose. A fractional one would make every expectation a rounding
/// question, and rounding is not what these tests are about.
/// </para>
/// </remarks>
internal sealed class ScaledTextMeasure(int factor) : ITextMeasure
{
    /// <summary>How many units one character and one line are worth.</summary>
    public int Factor { get; } = factor;

    public double Measure(string text, TextEmphasis emphasis)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Length * (double)Factor;
    }

    public double LineHeight => Factor;

    public double IndentWidth => 2 * Factor;

    public override string ToString() => "scaled x" + Factor.ToString(CultureInfo.InvariantCulture);
}
