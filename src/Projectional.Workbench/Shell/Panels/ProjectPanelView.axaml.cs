using Avalonia.Controls;

namespace Projectional.Workbench;

/// <summary>
/// The project tool: what is open and which documents and languages it consists of, as one tree of rows.
/// </summary>
/// <remarks>
/// The view is rows only. What a row means - which document it opens, whether a press expands it or selects it - is
/// the shell's, so nothing here knows about a <see cref="DocumentTab"/> or a language project.
/// </remarks>
internal sealed partial class ProjectPanelView : UserControl
{
    public ProjectPanelView() => InitializeComponent();

    /// <summary>The rows, in the order the explorer derived them.</summary>
    internal ListBox List => Rows;
}
