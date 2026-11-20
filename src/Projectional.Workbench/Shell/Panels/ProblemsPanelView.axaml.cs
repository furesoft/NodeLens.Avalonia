using Avalonia.Controls;

namespace Projectional.Workbench;

/// <summary>The problems tool: what the checks found in the document in front.</summary>
internal sealed partial class ProblemsPanelView : UserControl
{
    public ProblemsPanelView() => InitializeComponent();

    /// <summary>The rows the shell published for the active document.</summary>
    internal ListBox List => Rows;
}
