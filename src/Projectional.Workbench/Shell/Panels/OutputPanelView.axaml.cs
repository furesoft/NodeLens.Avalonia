using Avalonia.Controls;

namespace Projectional.Workbench;

/// <summary>The output tool: the inputs a run takes and the rows it produced.</summary>
internal sealed partial class OutputPanelView : UserControl
{
    public OutputPanelView() => InitializeComponent();

    /// <summary>The rows of the last run or generation.</summary>
    internal ListBox List => Rows;

    /// <summary>Where the user writes the inputs a run reads.</summary>
    internal TextBox Inputs => InputsBox;

    /// <summary>The button that runs the document in front.</summary>
    internal Button RunButton => Run;
}
