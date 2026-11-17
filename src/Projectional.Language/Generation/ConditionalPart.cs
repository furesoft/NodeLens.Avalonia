namespace Projectional.Language;

/// <summary>A choice between two templates, decided by one property's value.</summary>
/// <remarks>
/// The condition is a property and the text it must equal, never a delegate: a delegate could not be written into a
/// declared generator model, so the declared path would be weaker than C# and the whole point of ADR 0014 would be
/// lost. It is the same shape <c>ConditionalNotationCell</c> uses, and for the same reason.
/// </remarks>
public sealed record ConditionalPart : TemplatePart
{
    /// <summary>Creates a part that picks a template by <paramref name="slot"/>'s value.</summary>
    /// <param name="slot">The property to read.</param>
    /// <param name="value">The value that selects <paramref name="then"/>.</param>
    /// <param name="then">What to emit when the property holds <paramref name="value"/>.</param>
    /// <param name="otherwise">What to emit otherwise; <see langword="null"/> means nothing.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> or <paramref name="then"/> is <see langword="null"/>.
    /// </exception>
    public ConditionalPart(string slot, string value, TextTemplate then, TextTemplate? otherwise = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(then);

        Slot = slot;
        Value = value;
        Then = then;
        Otherwise = otherwise ?? TextTemplate.Empty;
    }

    /// <summary>The property to read.</summary>
    public string Slot { get; }

    /// <summary>The value that selects <see cref="Then"/>.</summary>
    public string Value { get; }

    /// <summary>What to emit when the property holds <see cref="Value"/>.</summary>
    public TextTemplate Then { get; }

    /// <summary>What to emit otherwise.</summary>
    public TextTemplate Otherwise { get; }

    public override string ToString() => $"if {Slot} = {Value}";
}
