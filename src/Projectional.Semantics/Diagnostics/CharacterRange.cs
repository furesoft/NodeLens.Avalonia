namespace Projectional.Semantics;

/// <summary>A stretch of one value's text, counted in characters from the start of that value.</summary>
/// <remarks>
/// <para>
/// The offsets are relative to the text of the <em>value</em> a diagnostic names, not to a line and not
/// to the document: a property holding <c>abc-def</c> reports the bad <c>def</c> as
/// <c>Of(4, 7)</c> whatever the notation puts before it. That is what lets a check describe its finding
/// without knowing how the concept is drawn, and what keeps the range valid when the notation around it
/// changes.
/// </para>
/// <para>
/// A range is a hint, never a requirement. A diagnostic without one underlines the whole cell it
/// resolves to, and a range that no longer fits the value is clamped by whoever draws it rather than
/// rejected here - the model may have been edited since the check ran, and a marker is not a reason to
/// throw.
/// </para>
/// </remarks>
public readonly record struct CharacterRange
{
    /// <summary>Describes <paramref name="length"/> characters from <paramref name="start"/>.</summary>
    /// <param name="start">The offset of the first character, counted from the start of the value.</param>
    /// <param name="length">How many characters the range covers; zero is an empty range at the start.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> or <paramref name="length"/> is negative.
    /// </exception>
    public CharacterRange(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        Start = start;
        Length = length;
    }

    /// <summary>The offset of the first character, counted from the start of the value.</summary>
    public int Start { get; }

    /// <summary>How many characters the range covers.</summary>
    public int Length { get; }

    /// <summary>The offset one past the last character.</summary>
    public int End => Start + Length;

    /// <summary>Describes the characters from <paramref name="start"/> up to but not including <paramref name="end"/>.</summary>
    /// <param name="start">The offset of the first character.</param>
    /// <param name="end">The offset one past the last character.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> is negative, or <paramref name="end"/> is before it.
    /// </exception>
    public static CharacterRange Of(int start, int end)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfLessThan(end, start);

        return new CharacterRange(start, end - start);
    }
}
