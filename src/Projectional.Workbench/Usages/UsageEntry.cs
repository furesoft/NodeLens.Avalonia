using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Workbench;

/// <summary>One row of the usages panel: where the reference is, which model holds it, and how to read it.</summary>
/// <param name="Model">The model that holds the reference, which is what choosing the row has to open.</param>
/// <param name="Place">The reference slot's cell, or the owner's node when the notation hides the slot.</param>
/// <param name="Text">The row as one line: what points at the target, and through which slot.</param>
/// <remarks>
/// The row carries a model rather than a node id alone because a usage may live in another document of the same
/// project, and a panel that could only jump inside the active one would have to hide the rest. The text is the whole
/// row, for the same reason <see cref="ProblemEntry"/> has one: a data template would name this type in XAML and widen
/// the app's surface for a string that is already the row.
/// </remarks>
internal sealed record UsageEntry(Model Model, CellAddress Place, string Text)
{
    public override string ToString() => Text;
}
