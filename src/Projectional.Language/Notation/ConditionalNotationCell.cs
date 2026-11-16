namespace Projectional.Language;

/// <summary>
/// Inline cells that are drawn only while a property of the node holds a particular value.
/// </summary>
/// <remarks>
/// <para>
/// A notation describes one drawing of a concept, and some of what a language writes is not always there: MPS
/// writes <c>abstract concept Rule</c> for an abstract concept and <c>concept Rule</c> for a concrete one, with the
/// same concept and the same notation. This is the cell that makes that difference expressible - the word and the
/// space after it exist in the notation always and in the tree only when the condition holds.
/// </para>
/// <para>
/// The condition is a property and a value rather than a predicate, because notation is data: a delegate could not
/// be written down in a notation model, so a language declared as a model and a language written in C# would stop
/// being the same language (ADR 0014). Comparing the property's text is the smallest form that covers the case
/// every notation has - a flag that turns a word on.
/// </para>
/// <para>
/// A conditional that does not hold produces <em>nothing</em>: not an empty cell, not a cell with no text. The
/// alternative was a zero-width cell, and a zero-width cell is a caret stop the user can walk into and type at,
/// pointing at an address that draws no text. Nothing is the honest answer, and it is what MPS does.
/// </para>
/// <para>
/// Inline only, for now. A block conditional is the same idea one level up, and it arrives when a notation needs to
/// hide a whole line rather than a word; adding it before something needs it would mean a runtime branch and a
/// validation rule nobody exercises.
/// </para>
/// </remarks>
public sealed class ConditionalNotationCell : InlineNotationCell
{
    /// <summary>Draws <paramref name="content"/> only while <paramref name="slot"/> holds <paramref name="value"/>.</summary>
    /// <param name="slot">The property the condition reads, which must be a property of the drawn concept.</param>
    /// <param name="value">The text the property has to equal, in its stored spelling.</param>
    /// <param name="content">The inline cells to draw while the condition holds.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or an element of <paramref name="content"/> is <see langword="null"/>.</exception>
    public ConditionalNotationCell(
        string slot,
        string value,
        params ReadOnlySpan<InlineNotationCell> content)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(value);

        Slot = slot;
        Value = value;
        Content = [.. content];

        foreach (var cell in Content)
        {
            ArgumentNullException.ThrowIfNull(cell);
        }
    }

    /// <summary>The property the condition reads.</summary>
    public string Slot { get; }

    /// <summary>The text <see cref="Slot"/> has to equal, which is the value's own stored spelling.</summary>
    /// <remarks>
    /// Compared ordinally, never parsed here: the type that owns the value already validated it, and a notation
    /// that re-parsed it would be a second opinion about what a value means.
    /// </remarks>
    public string Value { get; }

    /// <summary>The inline cells drawn while the condition holds.</summary>
    public IReadOnlyList<InlineNotationCell> Content { get; }

    public override string ToString() => $"if {Slot} is \"{Value}\" ({Content.Count})";
}
