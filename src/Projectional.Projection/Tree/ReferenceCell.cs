using Projectional.Core;

namespace Projectional.Projection;

/// <summary>
/// A reference slot, shown as the name of what it points at.
/// </summary>
/// <remarks>
/// The cell shows text, never the target's subtree. A reference is a link, so projecting the target
/// inline would duplicate the target's own editor and make editing through the reference ambiguous;
/// navigating to it is a separate action. <see cref="Target"/> is kept so that the editor can follow
/// the link without asking the model again during rendering.
/// </remarks>
public sealed class ReferenceCell : TextCell
{
    public ReferenceCell(
        CellAddress address,
        string slot,
        string text,
        ReferenceState state,
        NodeId? target = null)
        : base(address, text)
    {
        ArgumentException.ThrowIfNullOrEmpty(slot);

        if (state is ReferenceState.Resolved && target is null)
        {
            throw new ProjectionException(
                $"Reference cell for slot '{slot}' claims to be resolved but names no target.");
        }

        Slot = slot;
        State = state;
        Target = target;
    }

    public string Slot { get; }

    public ReferenceState State { get; }

    /// <summary>The node the slot points at, when it resolves.</summary>
    public NodeId? Target { get; }

    internal override string Describe() => $"reference {Slot}";
}
