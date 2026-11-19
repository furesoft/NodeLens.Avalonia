using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>One reference to the node a find-usages question is about: who holds it, in which slot.</summary>
/// <param name="Owner">The node whose reference slot is set.</param>
/// <param name="Slot">The name of that reference slot.</param>
/// <param name="Target">The node it points at.</param>
/// <remarks>
/// The place is computed rather than looked up: the projection addresses a reference slot by its owner and its slot
/// name, so the same address is correct in the document that draws the usage and in one that does not - which is what
/// lets a panel list usages across models before any of them is showing.
/// </remarks>
public readonly record struct UsageMatch(NodeId Owner, string Slot, NodeId Target)
{
    /// <summary>The cell a document draws this reference in.</summary>
    public CellAddress Place => CellAddress.OfSlot(Owner, Slot);

    public override string ToString() => $"{Owner}:{Slot}";
}
