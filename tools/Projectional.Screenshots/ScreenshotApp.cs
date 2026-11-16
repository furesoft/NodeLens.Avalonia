using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Projectional.Screenshots;

/// <summary>The application shell the capture window needs; it has no window content of its own.</summary>
internal sealed class ScreenshotApp(string output) : Application
{
    public override void Initialize()
    {
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
