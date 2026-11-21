namespace Projectional.TypingRules;

/// <summary>The keys of the typing language: the concepts a language's own typing rules are written with.</summary>
/// <remarks>
/// One concept, because a typing rule names rather than computes: which concept, how the text is recognised, the text
/// it is recognised by, and which property the text lands in. That is the same shape
/// <see cref="Projectional.Language.TypingRuleRegistry"/> holds at runtime, so nothing has to be interpreted.
/// </remarks>
public static class TypingConceptKeys
{
    /// <summary>One rule: which concept the typed text creates, and how that text is recognised.</summary>
    public const string Rule = "Typing.Rule";
}
