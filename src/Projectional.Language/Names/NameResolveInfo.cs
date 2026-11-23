using Projectional.Core;

namespace Projectional.Language;

/// <summary>The text a reference stored when it was written, which is what a name can fall back on.</summary>
/// <param name="Slot">The reference slot to read.</param>
/// <remarks>
/// <para>
/// The fallback beside <see cref="NameReference"/>, for the document that is loaded without the model its reference
/// points into: the target cannot be resolved, so its name cannot be asked, and the only text there is the
/// <see cref="NodeReference.ResolveInfo"/> the reference kept - the name the target had when the reference was written.
/// A body that reads both therefore names a node the same way whether or not the model it points at is open.
/// </para>
/// <para>
/// It answers nothing when the reference is not set, and nothing when it was set without a fallback text. The text may
/// be out of date - that is what it is, a degraded answer rather than a claim - which is why it belongs after
/// <see cref="NameReference"/> in a <see cref="NameFallback"/> rather than on its own.
/// </para>
/// </remarks>
public sealed record NameResolveInfo : NameBody
{
    /// <summary>Creates a part that answers the stored fallback text of <paramref name="slot"/>.</summary>
    /// <param name="slot">The reference slot to read.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public NameResolveInfo(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
    }

    /// <summary>The reference slot to read.</summary>
    public string Slot { get; }

    private protected override string Evaluate(Node node) =>
        node.Concept.FindReference(Slot) is not null
            ? node.GetReference(Slot)?.ResolveInfo ?? string.Empty
            : throw new InvalidOperationException(
                $"'{Slot}' is not a reference of {node.Concept.Key}, so a name body cannot read its stored text. The declared form refuses this at load.");

    public override string ToString() => $"resolveInfo {Slot}";
}
