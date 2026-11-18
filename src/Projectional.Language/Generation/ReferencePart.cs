namespace Projectional.Language;

/// <summary>A reference's target name, emitted as the projection would draw it.</summary>
/// <remarks>
/// <para>
/// A reference is one slot with three states, and all three are text: the target's name, the fallback a
/// dangling reference kept, and the word the notation uses when nothing is linked. This part emits exactly what
/// a <c>ReferenceCell</c> shows, so a generated file and the editor cannot disagree about what a reference says -
/// the same rule <see cref="PropertyPart"/> follows for a value, which is why the two are read from the model
/// rather than from the drawing.
/// </para>
/// <para>
/// A target without a name is emitted as the concept it is, in the framework's own spelling - the rule the
/// reference cell uses - because a reference always has something to say and an empty stretch would silently
/// drop a link from the output.
/// </para>
/// </remarks>
public sealed record ReferencePart : TemplatePart
{
    /// <summary>Creates a part that emits the name of the target of <paramref name="slot"/>.</summary>
    /// <param name="slot">The reference slot to read.</param>
    /// <param name="unsetText">What is emitted when nothing is linked.</param>
    /// <param name="unresolvedText">What is emitted when the target no longer exists.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="unsetText"/> or <paramref name="unresolvedText"/> is <see langword="null"/>.
    /// </exception>
    public ReferencePart(string slot, string unsetText = "<none>", string unresolvedText = "<unresolved>")
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(unsetText);
        ArgumentNullException.ThrowIfNull(unresolvedText);

        Slot = slot;
        UnsetText = unsetText;
        UnresolvedText = unresolvedText;
    }

    /// <summary>The reference slot to read.</summary>
    public string Slot { get; }

    /// <summary>What is emitted when nothing is linked.</summary>
    /// <remarks>
    /// A language states it because it is a word of that language: SQL writes nothing where a key is not linked,
    /// while a prose language writes <c>none</c>.
    /// </remarks>
    public string UnsetText { get; }

    /// <summary>What is emitted when the target no longer exists.</summary>
    public string UnresolvedText { get; }

    public override string ToString() => $"reference {Slot}";
}
