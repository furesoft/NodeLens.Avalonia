using Projectional.Core;

namespace Projectional.Language;

/// <summary>A part that is answered only while a slot holds something.</summary>
/// <param name="Slot">The slot whose filledness is the question.</param>
/// <param name="Part">What is answered when the slot is filled.</param>
/// <remarks>
/// <para>
/// The vocabulary could choose between two parts but not say "only when there is something": a
/// <see cref="NameChoice"/> compares a property's <em>value</em>, and a slot holding nothing matches no value at all.
/// This is the question the notation already asks with a <c>WhenFilled</c> cell, one level up - how a name says
/// "the kind, if one was written" without a part for every word the kind could be.
/// </para>
/// <para>
/// Filled means the slot holds something: a property with text, a child slot with at least one child, a reference that
/// was set - a dangling reference is set, because the author pointed somewhere. A property whose declaration carries a
/// default is therefore always filled, which is the honest reading of "the slot holds a value".
/// </para>
/// </remarks>
public sealed record NameWhenFilled : NameBody
{
    /// <summary>Creates a part that is answered only while <paramref name="slot"/> is filled.</summary>
    /// <param name="slot">The slot whose filledness is the question.</param>
    /// <param name="part">What is answered when the slot is filled.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="part"/> is <see langword="null"/>.</exception>
    public NameWhenFilled(string slot, NameBody part)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(part);

        Slot = slot;
        Part = part;
    }

    /// <summary>The slot whose filledness is the question.</summary>
    public string Slot { get; }

    /// <summary>What is answered when the slot is filled.</summary>
    public NameBody Part { get; }

    private protected override string Evaluate(Node node)
    {
        if (!node.Concept.TryGetSlot(Slot, out var slot))
        {
            throw new InvalidOperationException(
                $"'{Slot}' is not a slot of {node.Concept.Key}, so a name body cannot ask whether it is filled. The declared form refuses this at load.");
        }

        return IsFilled(node, slot) ? Part.NameOf(node) : string.Empty;
    }

    private static bool IsFilled(Node node, SlotDeclaration slot) => slot switch
    {
        PropertySlot property => node.GetOrNull(property.Name) is { Length: > 0 },
        ChildSlot => node.GetChildren(slot.Name).Count > 0,
        ReferenceSlot => node.GetReference(slot.Name) is not null,
        _ => false,
    };

    public override string ToString() => $"whenFilled {Slot}";
}
