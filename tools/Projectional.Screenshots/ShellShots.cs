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
    /// The sizes are the window's own rather than a crop, so there is enough page for a document and enough
    /// strip for the panel area's tabs and rows: the outline is a column, and Output, Problems and Usages are
    /// tabs of one area rather than three stacked panels.
    /// </remarks>
    internal static IReadOnlyList<ShellShot> All { get; } =
    [
        new("feature-run-output", ShowRunOutput, Width: 720, Height: 420),
        new("feature-generate-output", ShowGeneratedText, Width: 720, Height: 420),
        new("feature-run-tests", ShowTestRun, Width: 840, Height: 460),
        new("feature-import-file", ShowImportedDocument, Width: 720, Height: 540),
        new("feature-find-usages", ShowFindUsagesAndDefinition, Width: 1000, Height: 600),
        new("feature-project-explorer", ShowProjectExplorer, Width: 900, Height: 900),
        new("feature-outline", ShowOutline, Width: 720, Height: 560),
        new("feature-terminal", ShowTerminal, Width: 880, Height: 760),
    ];

    /// <summary>Shows the terminal tool running a program, which is the whole of what it is for.</summary>
    /// <remarks>
    /// The command is <c>dotnet --version</c> because the picture has to be reproducible: the repository pins the SDK in
    /// <c>global.json</c>, so the line it prints is a fact about this checkout rather than about the machine. Everything
    /// else in the image - the command echo, the exit notice - is what the panel itself writes when a program runs.
    /// The panel is shown and given a moment before the command starts, because a screen that is resized after it has
    /// content reflows that content, and a real user sees the panel before they type into it.
    /// </remarks>
    private static async Task ShowTerminal(WorkbenchView window)
    {
        window.ShowTerminalTool();

        await Task.Delay(250);

        window.RunInTerminal("dotnet --version");

        await Task.Delay(750);
    }

    /// <summary>Shows the outline of the sample document, which is a ruleset of several nested rules.</summary>
    /// <remarks>
    /// The tab is selected rather than the panel rebuilt: the picture is of the browser the shell shows, and which tab
    /// the left column is on is a piece of the shell's own state.
    /// </remarks>
    private static async Task ShowOutline(WorkbenchView window)
    {
        window.ShowOutlineTool();

        await Task.Delay(250);
    }

    /// <summary>The images of dialogs, which are windows of their own rather than states of the workbench.</summary>
    /// <remarks>
    /// The module settings dialog is photographed twice - its identity page and its dependency table - because the two
    /// answer different questions and one crop of both would answer neither.
    /// </remarks>
    internal static IReadOnlyList<DialogShot> Dialogs { get; } =
    [
        new("feature-module-settings", ShowModuleSettings, Width: 760, Height: 540),
        new("feature-module-dependencies", ShowModuleDependencies, Width: 760, Height: 540),
    ];

    /// <summary>Shows the settings of the pipeline sample's module.</summary>
    private static async Task<Window> ShowModuleSettings(MainWindow window)
    {
        var dialog = ModuleSettingsDialog.Open(window, OpenModules(window.Shell));

        await Task.Delay(250);

        return dialog;
    }

    /// <summary>Shows the same dialog on its dependency table, which is what a module is made of.</summary>
    private static async Task<Window> ShowModuleDependencies(MainWindow window)
    {
        var dialog = await ShowModuleSettings(window);

        dialog.FindControl<TabControl>("Pages")!.SelectedIndex = 1;

        await Task.Delay(250);

        return dialog;
    }

    /// <summary>Opens the three declared sample languages and answers the first of them.</summary>
    /// <remarks>
    /// One project cannot show every aspect - the pipeline has no types, scopes or intentions, the workflow has no
    /// constraints, imports or generators - so a picture of the tree or of a module's settings holds the three projects
    /// the repository ships.
    /// </remarks>
    private static LanguageProject OpenModules(WorkbenchView window)
    {
        LanguageProject? first = null;

        foreach (var name in (string[])["declared-pipeline", "declared-expressions", "declared-workflow"])
        {
            var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, name), out var error)
                ?? throw new InvalidOperationException(error);

            if (!window.OpenLanguage(language))
            {
                throw new InvalidOperationException($"The shell refused '{language.Name}'.");
            }

            first ??= language;
        }

        return first!;
    }

    /// <summary>Opens the three declared sample languages, so every aspect folder a language can have is listed.</summary>
    /// <remarks>
    /// The documents they open are closed again: the picture is the tree, and thirty definition documents listed above it
    /// would push the folders out of view.
    /// </remarks>
    private static async Task ShowProjectExplorer(WorkbenchView window)
    {
        OpenModules(window);

        var close = window.FindControl<MenuItem>("CloseDocumentItem")!;

        while (window.DocumentCount > 0)
        {
            Click(close);
        }

        // Every folder shut but one: the picture is about the aspect icons, which only stay on screen while the files
        // below them are not listed - and one folder is open so the badges a file row is drawn with are visible too.
        window.CollapseProjectFolders();
        window.ExpandProjectFolder(LanguageProject.StructureFolder);

        await Task.Delay(250);
    }

    /// <summary>Imports an ini-shaped file into a document of the language that declares a rule for it.</summary>
    /// <remarks>
    /// The file is the pipeline sample's ini variant, which no built-in format claims and no generator writes: the
    /// shell asks the opened language, its declared rule reads the text, and the picture is the document that comes
    /// out. <see cref="WorkbenchView.ImportPath"/> is the command the picker calls, so the image cannot show a document
    /// the command does not produce.
    /// </remarks>
    private static Task ShowImportedDocument(WorkbenchView window)
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
    /// own gesture: <see cref="WorkbenchView.OpenLanguage"/> is what the folder picker calls, and the menu entry is
    /// clicked rather than the work being redone here - so the picture cannot show a panel state the command does not
    /// produce.
    /// </remarks>
    private static async Task ShowTestRun(WorkbenchView window)
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
    private static async Task ShowRunOutput(WorkbenchView window)
    {
        window.RunInputs = "amount=1500";

        Click(window.FindControl<MenuItem>("RunDocumentItem")!);

        await Task.Delay(250);
    }

    /// <summary>Generates the document with its C# generator, so the rows are a code fragment.</summary>
    private static async Task ShowGeneratedText(WorkbenchView window)
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
    private static async Task ShowFindUsagesAndDefinition(WorkbenchView window)
    {
        window.OpenDocument(
            LanguageDocuments.StateMachine(),
            StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts),
            typing: StateMachineLanguage.TypingRules);

        await Task.Delay(250);

        var editor = window.ActiveEditor()!;
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
