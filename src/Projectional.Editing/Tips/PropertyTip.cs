using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// Decides what the tooltip over a property says: the description the language declared on that slot.
/// </summary>
/// <remarks>
/// <para>
/// A property is the one place in a projectional document where "nothing entered yet" is a visible state: the
/// notation always draws the value's cell, so an empty slot is a box the user can put the caret in but cannot
/// read. Documentation about what belongs there is exactly what a user needs at that moment, which is why this
/// tip appears over an empty value without being asked for.
/// </para>
/// <para>
/// A filled slot is the other case: once there is text, the text is the answer, and a box over it would be in the
/// way. A language that knows its text does not explain itself - an identifier, an operator, a key the notation
/// never spells out - says so on the slot with <see cref="PropertySlot.ShowDescriptionWhenFilled"/>, and the same
/// sentence is then shown over the value.
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
    /// Nothing to show means any of: the place is not a property, the property holds text and does not ask for the
    /// sentence over its value, the concept declares no description for that slot, or the node the pointer was over
    /// no longer exists. A pointer can outlive its node, and a tooltip must never throw at the moment the user
    /// moves the mouse.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    public static TipContent? Of(EditorSession session, CaretPosition position)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (position.Address.Slot is not { } slot
            || CaretNavigation.PlaceAt(session.Tree, position) is not { } place
            || place.Cell is not PropertyCell cell
            || session.Document.Model.Project.FindNode(position.Address.Owner) is not { } node
            || node.Concept.FindProperty(slot) is not { Description: { Length: > 0 } description } property)
        {
            return null;
        }

        return cell.Text.Length == 0 || property.ShowDescriptionWhenFilled
            ? new TipContent(slot, description)
            : null;
    }
}
