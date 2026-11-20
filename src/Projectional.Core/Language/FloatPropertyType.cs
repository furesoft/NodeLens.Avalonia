using System.Globalization;

namespace Projectional.Core;

/// <summary>A number with a fractional part.</summary>
/// <remarks>
/// <para>
/// The value is a <see cref="double"/>, not a <see cref="float"/>, and the name is still <c>float</c>: the name is
/// what a language author writes in a concept declaration, and <c>double</c> is the framework's floating-point
/// number. Narrowing to <see cref="float"/> would lose digits on every store-and-load for no gain.
/// </para>
/// <para>
/// The canonical spelling is the shortest text that reads back as the same number, which is what
/// <see cref="double.ToString(CultureInfo)"/> produces on this framework - so <c>1.5</c> is stored as
/// <c>1.5</c> and one and a half written with more zeros is not re-spelt behind the author's back. Parsing is as
/// tolerant as <c>int</c>'s, because the two are the framework's numbers and a reader should not have to learn two
/// rules.
/// </para>
/// <para>
/// <c>NaN</c> and the infinities are refused. They parse, and they would round-trip, but a value no arithmetic
/// comparison can answer is a mistake wherever it is stored rather than a number a slot should be able to hold - and
/// it is the one spelling whose meaning depends on the reader.
/// </para>
/// </remarks>
internal sealed class FloatPropertyType : PropertyType
{
    public FloatPropertyType()
        : base("float")
    {
    }

    public override bool TryParse(string? text, out object? value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    public override string Format(object value) => value is double number && double.IsFinite(number)
        ? number.ToString(CultureInfo.InvariantCulture)
        : throw new ArgumentException($"Expected a finite double but got '{value}'.", nameof(value));
}
