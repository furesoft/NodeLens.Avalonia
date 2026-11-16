namespace Projectional.Core;

/// <summary>One difference between two models, named by node id.</summary>
/// <remarks>
/// <para>
/// Not every member is meaningful for every <see cref="Kind"/>, and that is deliberate: one record keeps a diff
/// a flat list rather than a hierarchy of five types that every consumer would have to switch over anyway. What
/// each kind fills in:
/// </para>
/// <list type="table">
/// <listheader><term>Kind</term><description>Members</description></listheader>
/// <item><term><see cref="NodeDifferenceKind.Added"/></term>
/// <description><see cref="Slot"/> and <see cref="After"/>.</description></item>
/// <item><term><see cref="NodeDifferenceKind.Removed"/></term>
/// <description><see cref="Slot"/>, <see cref="Before"/>, and <see cref="OldValue"/> as the node's name, so a
/// badge or a tooltip can say what went without keeping the old model alive.</description></item>
/// <item><term><see cref="NodeDifferenceKind.Moved"/></term>
/// <description><see cref="Slot"/> as the slot it moved into, <see cref="After"/> and <see cref="Before"/>.</description></item>
/// <item><term><see cref="NodeDifferenceKind.PropertyChanged"/></term>
/// <description><see cref="Slot"/> and both values; an unset value is <see langword="null"/>, so clearing one
/// is a change too.</description></item>
/// <item><term><see cref="NodeDifferenceKind.ReferenceChanged"/></term>
/// <description><see cref="Slot"/> and both references as <see cref="NodeReference"/> text - the target id and
/// its fallback spelling, because a reference can be re-spelled without moving.</description></item>
/// </list>
/// </remarks>
/// <param name="Node">The node the difference is about, which is the same node in both models.</param>
/// <param name="Kind">How it differs.</param>
/// <param name="Slot">The property, reference or child slot it concerns, when the kind concerns one.</param>
/// <param name="Before">Where the node stood in the earlier model.</param>
/// <param name="After">Where the node stands in the later model.</param>
/// <param name="OldValue">The earlier property value, reference or name.</param>
/// <param name="NewValue">The later property value or reference.</param>
public readonly record struct NodeDifference(
    NodeId Node,
    NodeDifferenceKind Kind,
    string? Slot = null,
    NodePlace? Before = null,
    NodePlace? After = null,
    string? OldValue = null,
    string? NewValue = null)
{
    public override string ToString() => Kind switch
    {
        NodeDifferenceKind.Added => $"added {Node} at {After}",
        NodeDifferenceKind.Removed => $"removed {Node} from {Before}",
        NodeDifferenceKind.Moved => $"moved {Node} from {Before} to {After}",
        _ => $"{Kind} {Node}.{Slot}: '{OldValue}' -> '{NewValue}'",
    };
}
