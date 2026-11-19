namespace Projectional.IntentionRules;

/// <summary>The two sides a declared "insert" can put the new sibling on, as the symbols a model stores.</summary>
/// <remarks>
/// The spelling is the model's, not the enum's, and <see cref="IntentionLowering"/> is the only place that maps it
/// onto <see cref="Projectional.Editing.SiblingPlace"/>. Declaration order is the order completion offers them in.
/// </remarks>
public static class IntentionSiblings
{
    /// <summary>In the place the node holds, moving it and everything after it down one.</summary>
    public const string Before = "before";

    /// <summary>In the place after the node.</summary>
    public const string After = "after";
}
