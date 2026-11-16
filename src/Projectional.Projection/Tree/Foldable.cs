using Projectional.Core;

namespace Projectional.Projection;

/// <summary>Whether a node may be folded in the editor, which the language decides and the node answers.</summary>
/// <remarks>
/// <para>
/// The permission is model data, the fold is view state
/// ([collapsing.md](../../docs/architecture/collapsing.md)). This type answers the first half: a concept
/// nominates a boolean slot, and the node's own value says whether this node is foldable - so two nodes of
/// one concept can differ, and the answer survives a save, an undo and a rebuild.
/// </para>
/// <para>
/// It reads the <em>value</em>, never the presence of the slot. Property defaults are materialized, so the
/// model cannot tell "unset" from "set", and a slot declared with a default of <see langword="true"/>
/// therefore means "every node of this concept folds unless it opts out" - a legitimate design that needs
/// no code.
/// </para>
/// </remarks>
public static class Foldable
{
    /// <summary>Whether <paramref name="node"/> may be folded.</summary>
    /// <param name="node">The node to ask.</param>
    /// <returns>
    /// <see langword="true"/> when the concept nominates a collapse slot and this node's value is
    /// <see langword="true"/>. Everything else - no designation, no value, a value of false - is not foldable.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static bool Of(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Concept.CollapseSlot is { } slot
            && node.IsSet(slot.Name)
            && node.GetBool(slot.Name);
    }
}
