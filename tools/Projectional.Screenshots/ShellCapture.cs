using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

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
            }
        }

        return succeeded;
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
