using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Decides what the tooltip over an empty property says: the description the language declared on that slot.
/// </summary>
/// <remarks>
/// <para>
/// A property is the one place in a projectional document where "nothing entered yet" is a visible state: the
/// notation always draws the value's cell, so an empty slot is a box the user can put the caret in but cannot
/// read. Documentation about what belongs there is exactly what a user needs at that moment, which is why this
/// tip appears only while the value is empty - once there is text, the text is the answer and a box over it would
/// be in the way.
/// </para>
/// <para>
/// The tip is anchored to a <em>place</em> rather than a node, unlike <see cref="ConceptTip"/>: which slot the
/// pointer is over is the whole question, and a node has as many properties as its concept declares.
/// </para>
/// </remarks>
public static class PropertyTip
{
    /// <summary>What the tooltip for the property at <paramref name="position"/> says, or <see langword="null"/>.</summary>
    /// <param name="session">The document to read the node and the value from.</param>
    /// <param name="position">Where the pointer is, which names the node and the slot.</param>
    /// <returns>The slot's name and description, or <see langword="null"/> when there is nothing to show.</returns>
    /// <remarks>
    /// Nothing to show means any of: the place is not a property, the property already holds text, the concept
    /// declares no description for that slot, or the node the pointer was over no longer exists. A pointer can
    /// outlive its node, and a tooltip must never throw at the moment the user moves the mouse.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static TipContent? Of(EditorSession session, CaretPosition position)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (position.Address.Slot is not { } slot
            || CaretNavigation.PlaceAt(session.Tree, position) is not { } place
            || place.Cell is not PropertyCell { Text.Length: 0 })
        {
            return null;
        }

        return session.Document.Model.Project.FindNode(position.Address.Owner) is { } node
            && node.Concept.FindProperty(slot) is { Description: { Length: > 0 } description }
                ? new TipContent(slot, description)
                : null;
    }
}
