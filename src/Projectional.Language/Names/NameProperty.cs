using Projectional.Core;

namespace Projectional.Language;

/// <summary>A property's value, which is how a name that <em>is</em> a value is written.</summary>
/// <param name="Slot">The property slot to read.</param>
/// <remarks>
/// An unset property answers the empty string rather than a placeholder: a half-written node has no name, and the parts
/// that can omit an empty answer (<see cref="NameJoin"/>, <see cref="NameFallback"/>) are where "not there yet" turns
/// into "left out".
/// </remarks>
public sealed record NameProperty : NameBody
{
    /// <summary>Creates a part that answers the value of <paramref name="slot"/>.</summary>
    /// <param name="slot">The property slot to read.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public NameProperty(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
    }

    /// <summary>The property slot to read.</summary>
    public string Slot { get; }

    private protected override string Evaluate(Node node) =>
        node.Concept.FindProperty(Slot) is not null
            ? node.GetOrNull(Slot) ?? string.Empty
            : throw new InvalidOperationException(
                $"'{Slot}' is not a property of {node.Concept.Key}, so a name body cannot read it. The declared form refuses this at load.");

    public override string ToString() => $"property {Slot}";
}
