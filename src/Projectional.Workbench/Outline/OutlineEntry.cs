using Projectional.Core;

namespace Projectional.Workbench;

/// <summary>One row of the model browser: a node, how deep it sits, what to call it, and what it is drawn as.</summary>
/// <remarks>
/// <para>
/// A row names a node rather than a cell, because a browser is about the document's structure and not about where a
/// particular cell happens to be drawn: clicking a row means "take me to this thing", and the caret resolves the node
/// to its first place itself.
/// </para>
/// <para>
/// Whether the row is open is what the builder was told, not something the row changes - the same rule the project
/// explorer follows. A collapsed node's children are not part of the list at all, so a rebuilt list can never disagree
/// with the tree it describes.
/// </para>
/// </remarks>
internal sealed record OutlineEntry
{
    /// <summary>How far one level of nesting indents a row, in pixels.</summary>
    private const double IndentStep = 14;

    /// <summary>Creates a row.</summary>
    /// <param name="id">The node the row stands for, which is also what it is expanded by.</param>
    /// <param name="depth">How many levels below its root it sits, which is how far it is indented.</param>
    /// <param name="text">What the row says - the concept's own name and, when the node has one, its name.</param>
    /// <param name="concept">The concept's registry key, which decides the colour the row is drawn in.</param>
    /// <param name="hasChildren">Whether the node has anything under it, which is what draws a chevron.</param>
    /// <param name="isExpanded">Whether the row was drawn open.</param>
    internal OutlineEntry(
        NodeId id,
        int depth,
        string text,
        string? concept = null,
        bool hasChildren = false,
        bool isExpanded = true)
    {
        Id = id;
        Depth = depth;
        Text = text;
        Concept = concept;
        HasChildren = hasChildren;
        IsExpanded = isExpanded;
    }

    /// <summary>The node the row stands for.</summary>
    internal NodeId Id { get; }

    /// <summary>How deep the row sits.</summary>
    internal int Depth { get; }

    /// <summary>What the row says.</summary>
    internal string Text { get; }

    /// <summary>The concept's registry key, which is what the row's colour is derived from.</summary>
    internal string? Concept { get; }

    /// <summary>Whether the node has children, which is what draws a chevron.</summary>
    internal bool HasChildren { get; }

    /// <summary>Whether the row was drawn open.</summary>
    internal bool IsExpanded { get; }

    /// <summary>The letter the row is drawn as, which is the concept's initial.</summary>
    internal string Badge => OutlineIcons.BadgeOf(Concept);

    /// <summary>How far the row is indented, in pixels.</summary>
    internal double Indent => Depth * IndentStep;

    /// <summary>Whether the row is indented at all, so a spacer of width zero is not drawn.</summary>
    internal bool HasIndent => Depth > 0;

    /// <summary>The row as the browser draws it: indented by its depth.</summary>
    /// <remarks>
    /// The indentation is spaces rather than a tree control's own nesting because a row is a value: a test reads a list
    /// of them without a window, and the depth is what tells a reader which level a row is on.
    /// </remarks>
    public override string ToString() => new string(' ', Depth * 2) + Text;
}
