using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Projectional.Screenshots;

/// <summary>The application shell the capture window needs; it has no window content of its own.</summary>
/// <remarks>
/// The theme is loaded because the shell's shots are taken of a real <c>MainWindow</c>: its menu, buttons and
/// list rows are templated controls, and without a theme the window would render as unstyled boxes.
/// </remarks>
internal sealed class ScreenshotApp(string output) : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new CaptureWindow(output);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
