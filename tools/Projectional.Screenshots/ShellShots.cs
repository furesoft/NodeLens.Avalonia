using Avalonia.Controls;
using Avalonia.Interactivity;

using Projectional.Workbench;

namespace Projectional.Screenshots;

/// <summary>The catalogue of shell images: what a run and a generation put in the output panel.</summary>
/// <remarks>
/// Both are driven through the gestures the documentation names - the Run entry and the Generate submenu the
/// window builds from the document's own generators - so a picture cannot show a panel state the shell cannot
/// reach. The window opens its own sample document, so nothing here builds a model.
/// </remarks>
internal static class ShellShots
{
    /// <summary>Everything to capture, in the order the README presents the features.</summary>
    /// <remarks>
    /// The sizes are the window's own rather than a crop, so there is enough page for the document and enough
    /// panel for its rows but no empty third of a window: the outline is a column, the output panel is a strip.
    /// </remarks>
    internal static IReadOnlyList<ShellShot> All { get; } =
    [
        new("feature-run-output", ShowRunOutput, Width: 700, Height: 300),
        new("feature-generate-output", ShowGeneratedText, Width: 700, Height: 250),
    ];

    /// <summary>Runs the sample ruleset against <c>amount=1500</c>, which accepts and leaves a trace.</summary>
    private static async Task ShowRunOutput(MainWindow window)
    {
        window.FindControl<TextBox>("RunInputsInput")!.Text = "amount=1500";

        Click(window.FindControl<MenuItem>("RunDocumentItem")!);

        await Task.Delay(250);
    }

    /// <summary>Generates the document with its C# generator, so the rows are a code fragment.</summary>
    private static async Task ShowGeneratedText(MainWindow window)
    {
        var generate = window.FindControl<MenuItem>("GenerateItem")!;

        var entry = generate.Items.OfType<MenuItem>().FirstOrDefault(item => (item.Header as string) == "C Sharp")
            ?? throw new InvalidOperationException("The sample document declares no \"C Sharp\" generator.");

        entry.Command?.Execute(null);

        await Task.Delay(250);
    }

    /// <summary>Runs a menu entry the way a click does, without a pointer.</summary>
    private static void Click(MenuItem item) =>
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
}
