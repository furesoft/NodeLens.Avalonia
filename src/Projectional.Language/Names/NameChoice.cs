using Projectional.Core;

namespace Projectional.Language;

/// <summary>One of two parts, chosen by a property's value - the conditional of the name vocabulary.</summary>
/// <param name="Slot">The property slot to read.</param>
/// <param name="Value">The value that selects <paramref name="Then"/>.</param>
/// <param name="Then">What is answered when the property holds <paramref name="Value"/>.</param>
/// <param name="Otherwise">What is answered when it does not.</param>
/// <remarks>
/// A comparison against a text, not a computation: the same question <c>TextGen.Conditional</c> asks, so a language
/// states "when <c>kind</c> is <c>operation</c>, call it after its name" without a second way to read a property.
/// </remarks>
public sealed record NameChoice : NameBody
{
    /// <summary>Creates a part that picks between two bodies by a property's value.</summary>
    /// <param name="slot">The property slot to read.</param>
    /// <param name="value">The value that selects <paramref name="then"/>.</param>
    /// <param name="then">What is answered when the property holds <paramref name="value"/>.</param>
    /// <param name="otherwise">What is answered when it does not.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public NameChoice(string slot, string value, NameBody then, NameBody otherwise)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(then);
        ArgumentNullException.ThrowIfNull(otherwise);

        Slot = slot;
        Value = value;
        Then = then;
        Otherwise = otherwise;
    }

    /// <summary>The property slot to read.</summary>
    public string Slot { get; }

    /// <summary>The value that selects <see cref="Then"/>.</summary>
    public string Value { get; }

    /// <summary>What is answered when the property holds <see cref="Value"/>.</summary>
    public NameBody Then { get; }

    /// <summary>What is answered when it does not.</summary>
    public NameBody Otherwise { get; }

    private protected override string Evaluate(Node node)
    {
        var stored = node.Concept.FindProperty(Slot) is not null
            ? node.GetOrNull(Slot) ?? string.Empty
            : throw new InvalidOperationException(
                $"'{Slot}' is not a property of {node.Concept.Key}, so a name body cannot compare it. The declared form refuses this at load.");

        return string.Equals(stored, Value, StringComparison.Ordinal)
            ? Then.NameOf(node)
            : Otherwise.NameOf(node);
    }

    public override string ToString() => $"when {Slot} is \"{Value}\"";
}
