using Avalonia.Controls;

namespace Projectional.Workbench;

/// <summary>The outline tool: the concepts of the document in front, one row per node.</summary>
internal sealed partial class OutlinePanelView : UserControl
{
    public OutlinePanelView() => InitializeComponent();

    /// <summary>The rows the shell derived from the active document.</summary>
    internal ListBox List => Rows;
}
