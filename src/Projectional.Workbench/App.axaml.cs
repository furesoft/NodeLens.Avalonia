using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Projectional.Avalonia;

namespace Projectional.Workbench;

public sealed partial class App : Application
{
    /// <summary>
    /// What a single-view head composes the shell with, or <see langword="null"/> for the defaults.
    /// </summary>
    /// <remarks>
    /// Avalonia's browser backend has no desktop lifetime and no window: it gives the process one view, and the head
    /// that boots there says what storage, dialogs and capabilities that view gets. It is set once before
    /// <c>StartBrowserAppAsync</c>, which is why it is a property of the application rather than an argument.
    /// </remarks>
    public static Func<ShellOptions>? SingleViewOptions { get; set; }

    /// <summary>
    /// Called with the control a single-view head put in the view, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The head subscribes to what the shell reports - a saved file, a close request - through this, because the
    /// application owns the lifetime that creates the view and the head owns what to do with it.
    /// </remarks>
    public static Action<WorkbenchView>? SingleViewCreated { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var shell = new WorkbenchView(SingleViewOptions?.Invoke() ?? new ShellOptions());

            singleView.MainView = shell;
            SingleViewCreated?.Invoke(shell);
        }

        base.OnFrameworkInitializationCompleted();
    }
}