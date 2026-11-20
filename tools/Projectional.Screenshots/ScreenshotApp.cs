using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

using Dock.Avalonia.Themes.Fluent;

namespace Projectional.Screenshots;

/// <summary>The application shell the capture window needs; it has no window content of its own.</summary>
/// <remarks>
/// The themes are loaded because the shell's shots are taken of a real <c>MainWindow</c>: its menu, buttons and list rows
/// are templated controls, and the window's areas are dockables - without the docking theme a dock control has no
/// template at all and the work area renders empty, which is what a picture of "nothing" looks like.
/// </remarks>
internal sealed class ScreenshotApp(string output) : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new DockFluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new CaptureWindow(output);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
