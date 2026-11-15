namespace Projectional.Samples;

/// <summary>
/// The outcomes a <see cref="RulesConceptKeys.Rule"/> may reach, which is also the type of its
/// <c>outcome</c> property.
/// </summary>
/// <remarks>
/// A closed set rather than free text, so that mistyping an outcome is a rejected keystroke instead of
/// a rule nobody can act on. The spelling of a symbol is its stored value, so the words below are the
/// model.
/// </remarks>
public static class RuleOutcomes
{
    /// <summary>The rule lets the request through.</summary>
    public const string Accept = "Accept";

    /// <summary>The rule turns the request down.</summary>
    public const string Reject = "Reject";

    /// <summary>The rule wants a person to look at the request.</summary>
    public const string Review = "Review";
}
