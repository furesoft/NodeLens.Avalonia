namespace Projectional.Core;

/// <summary>
/// A slot pointing at a node that it does not own. A reference survives the node moving
/// elsewhere and only becomes a dangling marker when the target is deleted.
/// </summary>
/// <remarks>
/// References are single-valued by design, matching the change record that describes them:
/// one old target, one new target. A multi-valued relationship is modelled as a child node per
/// element so that it gets its own identity, order and change history.
/// </remarks>
public sealed record ReferenceSlot : SlotDeclaration
{
    public ReferenceSlot(string name, string targetConceptKey, bool allowOwnSubtree = false)
        : base(name)
    {
        if (!NameRules.IsValidConceptKey(targetConceptKey))
        {
            throw new ArgumentException($"'{targetConceptKey}' is not a valid concept key.", nameof(targetConceptKey));
        }

        TargetConceptKey = targetConceptKey;
        AllowOwnSubtree = allowOwnSubtree;
    }

    /// <summary>Every target has to be of this concept or extend it.</summary>
    public string TargetConceptKey { get; }

    /// <summary>
    /// Whether a reference may point into its own subtree. It normally must not, because a
    /// contained node cannot be shown to exist before its container does.
    /// </summary>
    public bool AllowOwnSubtree { get; }
}
