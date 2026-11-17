using Projectional.Core;

namespace Projectional.Workbench;

/// <summary>One row of the model browser: a node, how deep it sits, and what to call it.</summary>
/// <param name="Id">The node the row stands for.</param>
/// <param name="Depth">How many levels below its root it is, which is how far the row is indented.</param>
/// <param name="Text">What the row says - the concept's own name and, when the node has one, its name.</param>
/// <remarks>
/// A row names a node rather than a cell, because a browser is about the document's structure and not about where a
/// particular cell happens to be drawn: clicking a row means "take me to this thing", and the caret resolves the node
/// to its first place itself.
/// </remarks>
internal sealed record OutlineEntry(NodeId Id, int Depth, string Text)
{
    /// <summary>The row as the browser draws it: indented by its depth.</summary>
    /// <remarks>
    /// The indentation is spaces rather than a tree control's own nesting because the panel is a flat list: a tree
    /// widget would add expansion state to a browser that has no reason to hide anything yet, and the depth is what a
    /// reader actually uses.
    /// </remarks>
    public override string ToString() => new string(' ', Depth * 2) + Text;
}