using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>Decides what the tooltip over a node says, and how it is broken to fit the room it is given.</summary>
/// <remarks>
/// <para>
/// The description is documentation the language declared on the concept
/// ([concept-descriptions.md](../../docs/architecture/concept-descriptions.md)), so this type reads the
/// model and nothing else: no tree, no layout, no toolkit. Where the box goes is the marker tip's business
/// (<see cref="MarkerTip.Place"/>), which the concept tip reuses rather than duplicating.
/// </para>
/// <para>
/// A node whose concept declares no description answers <see langword="null"/> and shows no box at all. An
/// empty box that repeated the concept key would teach the user that hovering is pointless.
/// </para>
/// </remarks>
public static class ConceptTip
{
    /// <summary>What the tooltip for <paramref name="node"/> says, or <see langword="null"/> for none.</summary>
    /// <param name="node">The node the pointer is over.</param>
    /// <returns>The concept's key and its description, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public static ConceptTipContent? Of(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Concept.Description is { Length: > 0 } description
            ? new ConceptTipContent(node.Concept.Key, description)
            : null;
    }

    /// <summary>What the tooltip for the node <paramref name="nodeId"/> names says, or <see langword="null"/>.</summary>
    /// <param name="session">The document to read the node from.</param>
    /// <param name="nodeId">The node the pointer is over.</param>
    /// <returns>The concept's key and its description, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The id is resolved rather than trusted: a pointer can outlive the node it was over, and an id that no
    /// longer exists answers "nothing to show" instead of throwing at the moment the user moves the mouse.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static ConceptTipContent? Of(EditorSession session, NodeId nodeId)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.Document.Model.Project.FindNode(nodeId) is { } node ? Of(node) : null;
    }

    /// <summary>The lines to draw, the key first and the description broken to fit.</summary>
    /// <param name="content">What the tip says.</param>
    /// <param name="maxWidth">The widest a line may be, in layout units.</param>
    /// <param name="measure">What to measure a candidate line with.</param>
    /// <returns>The lines in reading order, never empty for a real content.</returns>
    /// <remarks>
    /// The key is broken like any other text, so a key longer than the box cannot make the measured width a
    /// lie. In practice it stays one line: a concept key is a name, not a sentence.
    /// </remarks>
    /// <exception cref="ArgumentNullException">A parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWidth"/> is not positive.</exception>
    public static IReadOnlyList<string> Lines(
        ConceptTipContent content,
        double maxWidth,
        ITextMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(measure);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);

        var lines = new List<string>(TipText.Wrap(content.ConceptKey, maxWidth, measure));

        lines.AddRange(TipText.Wrap(content.Description, maxWidth, measure));

        return lines;
    }
}
