namespace Projectional.Language;

/// <summary>A choice between two templates, decided by whether a slot holds anything.</summary>
/// <remarks>
/// <para>
/// <see cref="ConditionalPart"/> compares a property's value with a text; this asks a different question - is there
/// anything in this slot at all - and it asks it of every kind of slot, which is what makes three things expressible
/// that a template could not say before: brackets that close around a list only when it has an entry, a stand-in where
/// a value is missing, and a tree, where a node expands its children only once they exist.
/// </para>
/// <para>
/// Empty is not the same as absent here, and the model cannot tell them apart anyway: a property that declares a
/// default reads as filled, exactly as it draws as filled and as <see cref="PropertyPart"/> emits it.
/// </para>
/// <para>
/// The condition is a slot, never a delegate, for the reason <see cref="ConditionalPart"/> gives: a delegate could not
/// be written into a declared generator model, so the declared path would be the weaker one.
/// </para>
/// </remarks>
public sealed record WhenFilledPart : TemplatePart
{
    /// <summary>Creates a part that picks a template by whether <paramref name="slot"/> holds anything.</summary>
    /// <param name="slot">The slot to ask about: a property, a reference, a single child or a list.</param>
    /// <param name="then">What to emit when the slot holds something.</param>
    /// <param name="otherwise">What to emit otherwise; <see langword="null"/> means nothing.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="then"/> is <see langword="null"/>.</exception>
    public WhenFilledPart(string slot, TextTemplate then, TextTemplate? otherwise = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(then);

        Slot = slot;
        Then = then;
        Otherwise = otherwise ?? TextTemplate.Empty;
    }

    /// <summary>The slot to ask about.</summary>
    public string Slot { get; }

    /// <summary>What to emit when the slot holds something.</summary>
    public TextTemplate Then { get; }

    /// <summary>What to emit when it does not.</summary>
    public TextTemplate Otherwise { get; }

    public override string ToString() => $"when {Slot} is filled";
}
