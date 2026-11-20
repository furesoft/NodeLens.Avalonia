using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using Projectional.Avalonia;
using Projectional.Editing;
using Projectional.Languages;
using Projectional.Projection;
using Projectional.Samples.StateMachine;
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
        new("feature-run-tests", ShowTestRun, Width: 820, Height: 320),
        new("feature-import-file", ShowImportedDocument, Width: 700, Height: 470),
        new("feature-find-usages", ShowFindUsagesAndDefinition, Width: 1000, Height: 520),
    ];

    /// <summary>Imports an ini-shaped file into a document of the language that declares a rule for it.</summary>
    /// <remarks>
    /// The file is the pipeline sample's ini variant, which no built-in format claims and no generator writes: the
    /// shell asks the opened language, its declared rule reads the text, and the picture is the document that comes
    /// out. <see cref="MainWindow.ImportPath"/> is the command the picker calls, so the image cannot show a document
    /// the command does not produce.
    /// </remarks>
    private static Task ShowImportedDocument(MainWindow window)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "declared-pipeline");

        var language = LanguageProject.Open(directory, out var error)
            ?? throw new InvalidOperationException(error);

        if (!window.OpenLanguage(language))
        {
            throw new InvalidOperationException($"The shell refused '{language.Name}'.");
        }

        window.ImportPath(Path.Combine(directory, "variant.ini"));

        return Task.Delay(250);
    }

    /// <summary>Runs the declared sample's tests folder and shows the rows the command puts in the panel.</summary>
    /// <remarks>
    /// The language project is loaded from the copy of the sample in this tool's output, because a project is loaded
    /// from a folder and a picture of running a folder's tests has to run that folder. Everything else is the shell's
    /// own gesture: <see cref="MainWindow.OpenLanguage"/> is what the folder picker calls, and the menu entry is
    /// clicked rather than the work being redone here - so the picture cannot show a panel state the command does not
    /// produce.
    /// </remarks>
    private static async Task ShowTestRun(MainWindow window)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "declared-pipeline");

        var language = LanguageProject.Open(directory, out var error)
            ?? throw new InvalidOperationException(error);

        if (!window.OpenLanguage(language))
        {
            throw new InvalidOperationException($"The shell refused '{language.Name}'.");
        }

        var path = Path.Combine(directory, "pipeline.mps");

        var document = DocumentFile.TryLoad(path, language.NewProject(), out error)
            ?? throw new InvalidOperationException(error);

        window.OpenDocument(document, language.Notations, path);

        Click(window.FindControl<MenuItem>("RunTestsItem")!);

        await Task.Delay(250);
    }

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

    /// <summary>
    /// Opens a state machine, asks who points at its initial state, and then goes to that declaration.
    /// </summary>
    /// <remarks>
    /// The document is the worked example rather than the shell's own ruleset, because a ruleset has no reference to
    /// show: the usages panel is only worth a picture when it has rows, and Ctrl+B only means something on a use.
    /// Both gestures are the real ones - Alt+F7 on the window, Ctrl+B on the editor - so the image is the state a user
    /// reaches, not one arranged by hand.
    /// </remarks>
    private static async Task ShowFindUsagesAndDefinition(MainWindow window)
    {
        window.OpenDocument(
            LanguageDocuments.StateMachine(),
            StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts),
            typing: StateMachineLanguage.TypingRules);

        await Task.Delay(250);

        var tab = (TabItem)window.FindControl<TabControl>("Documents")!.SelectedItem!;
        var editor = (ProjectionalEditorControl)((ScrollViewer)tab.Content!).Content!;
        var session = editor.Session!;

        var reference = session.Tree.Cells()
            .OfType<ReferenceCell>()
            .First(cell => cell.Slot == "source" && cell.State == ReferenceState.Resolved);

        session.Select(Selection.At(reference.Address));
        editor.InvalidateVisual();

        // The shell's gesture: the panel lists every reference to the node the caret names.
        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.F7,
            KeyModifiers = KeyModifiers.Alt,
            Source = window,
        });

        await Task.Delay(250);

        // The editor's gesture: the caret moves to the declaration the panel is about, with its name selected.
        editor.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.B,
            KeyModifiers = KeyModifiers.Control,
            Source = editor,
        });

        await Task.Delay(250);
    }

    /// <summary>Runs a menu entry the way a click does, without a pointer.</summary>
    private static void Click(MenuItem item) =>
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
}
