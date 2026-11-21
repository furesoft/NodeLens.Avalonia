using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Projectional.Languages;
using Projectional.Samples.StateMachine;
using Projectional.Workbench;

namespace Projectional.Screenshots;

/// <summary>Takes the shell's own shots, one window per image.</summary>
/// <remarks>
/// The window is shown rather than only arranged, because a panel's height comes from a template the theme
/// supplies: an unshown window has no menu, no list rows and no tab strip, and the picture would be of a
/// window nobody can see. Each window is closed after its shot, and a failing shot is logged instead of
/// taking the rest of the set down with it.
/// </remarks>
internal static class ShellCapture
{
    /// <summary>Captures every shell shot into <paramref name="output"/> and answers what failed.</summary>
    internal static async Task<bool> RunAsync(string output, List<string> log)
    {
        var succeeded = true;

        foreach (var shot in ShellShots.All)
        {
            try
            {
                var size = await CaptureAsync(shot, output);

                log.Add($"{shot.FileName}.png {size.Width}x{size.Height}");
            }
            catch (Exception exception)
            {
                succeeded = false;
                log.Add($"{shot.FileName}: {exception}");

                // Said out loud as well as logged: the log is a file nobody opens, and a shot that failed used to leave
                // the previous picture in place with nothing on the console to say so.
                Console.Error.WriteLine($"{shot.FileName} was not written: {exception.Message}");
            }
        }

        foreach (var shot in ShellShots.Dialogs)
        {
            try
            {
                var size = await CaptureDialogAsync(shot, output);

                log.Add($"{shot.FileName}.png {size.Width}x{size.Height}");
            }
            catch (Exception exception)
            {
                succeeded = false;
                log.Add($"{shot.FileName}: {exception}");
                Console.Error.WriteLine($"{shot.FileName} was not written: {exception.Message}");
            }
        }

        // After the pictures, because a gesture check opens a window of its own. These are the parts of the shell that
        // are wiring rather than drawing: a chevron that does nothing looks exactly like a chevron that works, so the
        // only way to know is to press it and read the row afterwards.
        succeeded &= await CheckAsync("project chevron", CheckProjectChevronAsync, log);
        succeeded &= await CheckAsync("outline chevron", CheckOutlineChevronAsync, log);
        succeeded &= await CheckAsync("new document follows the language", CheckNewDocumentAsync, log);

        return succeeded;
    }

    /// <summary>Runs one gesture check and answers whether it held.</summary>
    /// <remarks>
    /// A check is a picture's opposite: it proves nothing by looking right, so a failure is a message and an exit code
    /// rather than a wrong image. Both are reported the way the shots are, because the log is what a run leaves behind
    /// and a check that failed silently would be a check nobody has.
    /// </remarks>
    private static async Task<bool> CheckAsync(string name, Func<MainWindow, Task> check, List<string> log)
    {
        try
        {
            await RunCheckAsync(check);

            log.Add($"{name}: passed");

            return true;
        }
        catch (Exception exception)
        {
            log.Add($"{name}: {exception}");

            Console.Error.WriteLine($"{name} failed: {exception.Message}");

            return false;
        }
    }

    /// <summary>Shows a window, runs a gesture check against it and closes it.</summary>
    /// <remarks>
    /// The window is shown for the same reason a shot's is: a row's chevron is part of a template the theme supplies, and
    /// a window that was never shown has no rows to press.
    /// </remarks>
    private static async Task RunCheckAsync(Func<MainWindow, Task> check)
    {
        var window = new MainWindow
        {
            Width = 980,
            Height = 700,
            Position = new PixelPoint(0, 0),
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        try
        {
            window.Show();
            await SettleAsync();

            await check(window);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Presses a project folder's chevron and asserts that the folder shut.</summary>
    /// <remarks>
    /// The bug this pins: the handler is attached to the list, so <c>sender</c> is the list and the row lives on the
    /// event's source. Reading the sender meant the row was never found and the press did nothing at all - no
    /// exception, no log line, a chevron that ignored the pointer. Reading the source is what makes it work, and the
    /// row is read back afterwards so that "the folder shut" is what was observed rather than what was assumed.
    /// </remarks>
    private static async Task CheckProjectChevronAsync(MainWindow window)
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "declared-pipeline"), out var error)
            ?? throw new InvalidOperationException(error);

        if (!window.OpenLanguage(language))
        {
            throw new InvalidOperationException("the shell refused the pipeline project");
        }

        await SettleAsync();

        var list = window.ProjectList;

        var row = list.Items.OfType<ProjectEntry>()
            .FirstOrDefault(entry => entry.Kind is ProjectNode.Folder && entry.IsExpanded)
            ?? throw new InvalidOperationException("the project explorer showed no open folder to shut");

        PressChevron(list, row);

        var after = list.Items.OfType<ProjectEntry>().FirstOrDefault(entry => entry.Key == row.Key)
            ?? throw new InvalidOperationException($"the row '{row.Text}' disappeared when its chevron was pressed");

        if (after.IsExpanded)
        {
            throw new InvalidOperationException($"pressing the chevron of '{row.Text}' did not shut it");
        }
    }

    /// <summary>Presses a model browser row's chevron and asserts that the node shut.</summary>
    /// <remarks>
    /// The same correction as the project explorer's, so the same check: the handler reads the row from the event's
    /// source, and the row is read back afterwards rather than assumed to have changed.
    /// </remarks>
    private static async Task CheckOutlineChevronAsync(MainWindow window)
    {
        window.OpenDocument(
            LanguageDocuments.StateMachine(),
            StateMachineLanguage.CreateNotations(StateMachineLanguage.Concepts),
            typing: StateMachineLanguage.TypingRules);

        window.ShowOutlineTool();

        await SettleAsync();

        var list = window.OutlineList;

        var row = list.Items.OfType<OutlineEntry>()
            .FirstOrDefault(entry => entry.HasChildren && entry.IsExpanded)
            ?? throw new InvalidOperationException("the model browser showed no open row to shut");

        PressChevron(list, row);

        var after = list.Items.OfType<OutlineEntry>().FirstOrDefault(entry => entry.Id == row.Id)
            ?? throw new InvalidOperationException($"the row '{row.Text}' disappeared when its chevron was pressed");

        if (after.IsExpanded)
        {
            throw new InvalidOperationException($"pressing the chevron of '{row.Text}' did not shut it");
        }
    }

    /// <summary>Presses New ▸ Document and asserts that the document belongs to the opened language.</summary>
    /// <remarks>
    /// The failure this pins: the command opened the shell's own sample whatever folder was open, so a window working
    /// in a declared language got a ruleset. A document that opens is a document either way, which is why this is a
    /// gesture check and not a picture.
    /// </remarks>
    private static async Task CheckNewDocumentAsync(MainWindow window)
    {
        var language = LanguageProject.Open(Path.Combine(AppContext.BaseDirectory, "declared-pipeline"), out var error)
            ?? throw new InvalidOperationException(error);

        if (!window.OpenLanguage(language))
        {
            throw new InvalidOperationException("the shell refused the pipeline project");
        }

        await SettleAsync();

        window.NewDocumentCommand();
        await SettleAsync();

        var root = window.ActiveEditor()?.Session?.Document
            ?? throw new InvalidOperationException("New ▸ Document opened no document");

        if (!root.Concept.IsKindOf("Pipeline.Pipeline"))
        {
            throw new InvalidOperationException(
                $"New ▸ Document opened a '{root.Concept.Key}' instead of a document of the opened language");
        }
    }

    /// <summary>Presses the chevron a row is drawn with, the way a pointer does.</summary>
    /// <remarks>
    /// The button is looked up through the row's own <c>DataContext</c> rather than through a position, because a row is
    /// a value and the template's button is the only thing carrying it. Raising the click on the button - not on the
    /// list - is what makes the event travel the path a real press travels, so the list's handler is reached the way it
    /// is in use.
    /// </remarks>
    private static void PressChevron(ListBox list, object row)
    {
        var button = list.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(found => found.Classes.Contains("chevron") && ReferenceEquals(found.DataContext, row))
            ?? throw new InvalidOperationException($"the row '{row}' has no chevron on screen");

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    }

    /// <summary>Shows the workbench, opens one of its dialogs over it and writes the image of the dialog.</summary>
    /// <remarks>
    /// The dialog is opened rather than waited for: a modal one returns only when it closes, so the picture has to be
    /// taken between the two. The workbench behind it is shown because a dialog takes the theme variant of the window it
    /// belongs to, and a picture taken over nothing would be of a dialog in whichever variant it defaulted to.
    /// </remarks>
    private static async Task<PixelSize> CaptureDialogAsync(DialogShot shot, string output)
    {
        var window = new MainWindow
        {
            Width = 900,
            Height = 700,
            Position = new PixelPoint(0, 0),
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        Window? dialog = null;

        try
        {
            window.Show();
            await SettleAsync();

            Click(window.FindControl<MenuItem>("LightThemeItem")!);
            await SettleAsync();

            dialog = await shot.Open(window);

            dialog.Width = shot.Width;
            dialog.Height = shot.Height;
            await SettleAsync();

            // Focus would start the caret's blink timer in the page behind the dialog, which the next run's picture would
            // not have.
            dialog.FocusManager?.Focus(null);
            await SettleAsync();

            using var bitmap = ImageWriter.Render(dialog);

            return ImageWriter.Write(bitmap, Path.Combine(output, $"{shot.FileName}.png"));
        }
        finally
        {
            dialog?.Close();
            window.Close();
        }
    }

    /// <summary>Shows one window, drives it into the shot's state and writes the image.</summary>
    private static async Task<PixelSize> CaptureAsync(ShellShot shot, string output)
    {
        var window = new MainWindow
        {
            Width = shot.Width,
            Height = shot.Height,
            Position = new PixelPoint(0, 0),
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        try
        {
            window.Show();
            await SettleAsync();

            // The window follows the desktop by default, and the gallery is light: one press of the
            // View menu's entry is how a user makes the same choice.
            Click(window.FindControl<MenuItem>("LightThemeItem")!);
            await SettleAsync();

            await shot.Arrange(window);

            // Without this the caret would blink, and a picture taken 200 ms later would show a caret the next
            // run does not: focus is what starts the blink timer, so the shot is taken with none.
            window.FocusManager?.Focus(null);
            await SettleAsync();

            using var bitmap = ImageWriter.Render(window);

            return ImageWriter.Write(bitmap, Path.Combine(output, $"{shot.FileName}.png"));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Runs a menu entry the way a click does, without a pointer.</summary>
    private static void Click(MenuItem item) =>
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));

    /// <summary>Lets layout, render and any dispatcher work catch up before the next read.</summary>
    private static async Task SettleAsync()
    {
        await Task.Delay(60);

        Dispatcher.UIThread.RunJobs();

        await Task.Delay(60);
    }
}
