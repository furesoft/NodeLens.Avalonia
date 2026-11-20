using Avalonia.Controls;

namespace Projectional.Workbench;

/// <summary>The outline tool: the structure of the document in front, one row per node.</summary>
/// <remarks>
/// Rows only. What a row means - which node it opens, what its chevron does, what the two actions in the header act on -
/// is the shell's, so nothing here knows about a session or about a model.
/// </remarks>
internal sealed partial class OutlinePanelView : UserControl
{
    public OutlinePanelView() => InitializeComponent();

    /// <summary>The rows the shell derived from the active document.</summary>
    internal ListBox List => Rows;
}
