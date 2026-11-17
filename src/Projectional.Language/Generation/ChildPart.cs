namespace Projectional.Language;

/// <summary>The generated text of the single child in a slot.</summary>
/// <remarks>
/// An empty slot is a failure rather than empty text: a template that names a child it needs cannot produce text
/// without it, and a silent gap would be a generated file that looks right and is not.
/// </remarks>
public sealed record ChildPart : TemplatePart
{
    /// <summary>Creates a part that expands the child in <paramref name="slot"/>.</summary>
    /// <param name="slot">The child slot to expand.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    public ChildPart(string slot)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        Slot = slot;
    }

    /// <summary>The child slot to expand.</summary>
    public string Slot { get; }

    public override string ToString() => $"child {Slot}";
}
