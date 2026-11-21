namespace Projectional.TypingRules;

/// <summary>One typing rule as a document states it, before it becomes a predicate.</summary>
/// <param name="ConceptKey">The concept a burst of typing creates.</param>
/// <param name="Claims">How the text is recognised - one of <see cref="TypingClaims"/>.</param>
/// <param name="Text">The prefix or the words <paramref name="Claims"/> reads, or <see langword="null"/>.</param>
/// <param name="PropertySlot">The property the text is written into, or <see langword="null"/> for none.</param>
/// <remarks>
/// The registry the editor asks holds a <see cref="Delegate"/>, which cannot be read back - so a tool that has to
/// <em>write</em> the rules again (the C# export) needs the model's own words. This is that shape, and it is the
/// lowering's input and output rather than a second description that could drift from the first: the registry is
/// built from exactly these.
/// </remarks>
public readonly record struct DeclaredTypingRule(
    string ConceptKey,
    string Claims,
    string? Text = null,
    string? PropertySlot = null);
