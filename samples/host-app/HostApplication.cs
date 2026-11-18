using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace HostApp;

/// <summary>The Avalonia application: it owns one window and nothing else.</summary>
internal sealed class HostApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new HostWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
