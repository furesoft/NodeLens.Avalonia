using Avalonia.Controls;

namespace Projectional.Workbench;

/// <summary>What the document area shows while no document is open.</summary>
/// <remarks>
/// A view and not a sentence composed in the layout, because the hint it gives names the gestures a reader has to know -
/// and those are the shell's promise, not the docking library's.
/// </remarks>
internal sealed partial class EmptyStateView : UserControl
{
    public EmptyStateView() => InitializeComponent();
}
