using Projectional.Projection;
using Projectional.Semantics;

namespace Projectional.Workbench;

/// <summary>One row of the problems panel: what a check found, and where to go to fix it.</summary>
/// <param name="Place">The cell the row jumps to - the finding's slot when it named one, else its node.</param>
/// <param name="Severity">How much it matters, which is the order the rows are read in.</param>
/// <param name="Text">The finding as one line: its message, then what it is about.</param>
/// <remarks>
/// The row carries a <see cref="CellAddress"/> rather than a node id because a finding about a slot is a finding about
/// a place, and the panel's whole job after reporting is to put the caret there. It carries no undo or edit
/// knowledge: a fix is applied through the editor's own offer list, which resolves it against the current findings
/// rather than against a copy of them.
/// </remarks>
internal sealed record ProblemEntry(CellAddress Place, DiagnosticSeverity Severity, string Text)
{
    /// <summary>The severity as a word, because the panel is read rather than compared.</summary>
    public string SeverityLabel => Severity switch
    {
        DiagnosticSeverity.Error => "Error",
        DiagnosticSeverity.Warning => "Warning",
        DiagnosticSeverity.Info => "Info",
        _ => "Hint",
    };

    /// <summary>The row as one line, which is what the panel's list draws.</summary>
    /// <remarks>
    /// A <c>ToString</c> rather than a data template on purpose: the panel has no per-severity styling yet, and a
    /// template would have to name this type in XAML - which means widening the app's surface for a string that is
    /// already the whole row.
    /// </remarks>
    public override string ToString() => $"{SeverityLabel}: {Text}";
}