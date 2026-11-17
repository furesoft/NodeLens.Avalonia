using Projectional.Core;

namespace Projectional.Editing;

/// <summary>The place a fragment goes: a parent, one of its child slots, and an index in that slot.</summary>
/// <remarks>
/// One value rather than three out-parameters, so the decision that reads the caret and the edit that uses it
/// cannot drift apart: <see cref="PasteFragmentAction"/> fills it once and then inserts at exactly what it
/// decided.
/// </remarks>
/// <param name="Parent">The node that gains the fragment.</param>
/// <param name="Slot">The child slot of <paramref name="Parent"/> that receives it.</param>
/// <param name="Index">Where among the slot's children the first root goes.</param>
internal readonly record struct PastePlace(Node Parent, string Slot, int Index);
