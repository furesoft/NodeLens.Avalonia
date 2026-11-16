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
    public static TipContent? Of(Node node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node.Concept.Description is { Length: > 0 } description
            ? new TipContent(node.Concept.Key, description)
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
    public static TipContent? Of(EditorSession session, NodeId nodeId)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.Document.Model.Project.FindNode(nodeId) is { } node ? Of(node) : null;
    }
}
