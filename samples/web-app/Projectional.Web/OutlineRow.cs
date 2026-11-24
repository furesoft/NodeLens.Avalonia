using Projectional.Core;

namespace Projectional.Web;

/// <summary>One row of the model browser: a node, how deep it sits, and what to call it.</summary>
/// <param name="Id">The node the row stands for, which is also what expands it.</param>
/// <param name="Depth">How many levels below its root it sits.</param>
/// <param name="Text">What the row says: the concept's short name and, when the node has one, its name.</param>
/// <param name="ConceptKey">The concept's registry key, which is what a host colours the row by.</param>
/// <param name="HasChildren">Whether the node has anything under it, which is what draws a chevron.</param>
/// <param name="IsExpanded">Whether the row was built open.</param>
public sealed record OutlineRow(
    NodeId Id,
    int Depth,
    string Text,
    string ConceptKey,
    bool HasChildren,
    bool IsExpanded)
{
    public override string ToString() => $"{new string(' ', Depth * 2)}{Text}";
}
