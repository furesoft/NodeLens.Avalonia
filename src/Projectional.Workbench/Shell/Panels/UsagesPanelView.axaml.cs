using Avalonia.Controls;

namespace Projectional.Workbench;

/// <summary>The usages tool: every reference to the node the caret names.</summary>
internal sealed partial class UsagesPanelView : UserControl
{
    public UsagesPanelView() => InitializeComponent();

    /// <summary>The rows of the last search.</summary>
    internal ListBox List => Rows;
}
