namespace Projectional.ConstraintRules;

/// <summary>The three severities a declared rule can report under, as the symbols a model stores.</summary>
/// <remarks>
/// The spelling is the model's, not the enum's: a rule is data, so the symbols are what an author types and what a
/// file contains, and <see cref="ConstraintLowering"/> is the only place that maps them onto the framework's
/// severity. Declaration order is the order completion offers them in.
/// </remarks>
public static class ConstraintSeverities
{
    /// <summary>Something that is wrong.</summary>
    public const string Error = "error";

    /// <summary>Something that is probably wrong.</summary>
    public const string Warning = "warning";

    /// <summary>Something worth pointing out.</summary>
    public const string Hint = "hint";
}
