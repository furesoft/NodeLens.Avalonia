namespace Projectional.IntentionRules;

/// <summary>The four directions a declared move can name, as the symbols a model stores.</summary>
/// <remarks>
/// The spelling is the model's, not the enum's, and <see cref="IntentionLowering"/> is the only place that maps it
/// onto <see cref="Projectional.Editing.NodeMove"/>. The extremes are named rather than "several steps", because an
/// entry that says where the node ends up must land there in one edit.
/// </remarks>
public static class IntentionMoves
{
    /// <summary>Towards the front of the slot.</summary>
    public const string Up = "up";

    /// <summary>Towards the end of the slot.</summary>
    public const string Down = "down";

    /// <summary>To the first place of the slot.</summary>
    public const string ToFront = "front";

    /// <summary>To the last place of the slot.</summary>
    public const string ToBack = "back";
}
