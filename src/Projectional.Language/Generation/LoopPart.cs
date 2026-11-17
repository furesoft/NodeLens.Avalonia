namespace Projectional.Language;

/// <summary>One repetition per child of a list slot, with a body that says what goes around it.</summary>
/// <remarks>
/// <para>
/// <see cref="ChildrenPart"/> joins the children's own text; a loop exists for the case where each child needs
/// something of its own around it - a label, a keyword, punctuation. The body is applied to <em>each child</em>, so
/// inside it a <see cref="SelfPart"/> is that child's own text and a <see cref="PropertyPart"/> reads the child's
/// property.
/// </para>
/// <para>
/// The body may not name a counter or an index: a template has no variables, and a loop that pretended to have one
/// would be a programming language hiding in a data structure.
/// </para>
/// </remarks>
public sealed record LoopPart : TemplatePart
{
    /// <summary>Creates a part that repeats <paramref name="body"/> per child of <paramref name="slot"/>.</summary>
    /// <param name="slot">The list slot to walk.</param>
    /// <param name="body">What to emit per child, applied to that child.</param>
    /// <param name="separator">What goes between two repetitions; <see langword="null"/> means nothing.</param>
    /// <exception cref="ArgumentException"><paramref name="slot"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> is <see langword="null"/>.</exception>
    public LoopPart(string slot, TextTemplate body, TextTemplate? separator = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);
        ArgumentNullException.ThrowIfNull(body);

        Slot = slot;
        Body = body;
        Separator = separator ?? TextTemplate.Empty;
    }

    /// <summary>The list slot to walk.</summary>
    public string Slot { get; }

    /// <summary>What to emit per child, applied to that child.</summary>
    public TextTemplate Body { get; }

    /// <summary>What goes between two repetitions.</summary>
    public TextTemplate Separator { get; }

    public override string ToString() => $"loop {Slot}";
}
