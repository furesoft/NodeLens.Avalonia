namespace Projectional.Language;

/// <summary>A property's value, emitted as it is stored.</summary>
/// <remarks>
/// The value is emitted verbatim: no quoting, no escaping, no culture. A template that needs punctuation around it
/// writes the punctuation, which is what keeps "how a value is spelled" a decision of the language rather than a
/// convention of the framework - and what keeps generation deterministic on every machine.
/// </remarks>
public sealed record PropertyPart : TemplatePart
{
    /// <summary>Creates a part that emits the value of <paramref name="slot"/>.</summary>
    /// <param name="slot">The property slot to read.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public PropertyPart(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
    }

    /// <summary>The property slot to read.</summary>
    public string Slot { get; }

    public override string ToString() => $"property {Slot}";
}
