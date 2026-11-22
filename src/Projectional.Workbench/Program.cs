using Avalonia;

namespace Projectional.Workbench;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (SelfCheck.IsRequested(args))
        {
            Environment.ExitCode = SelfCheck.Run();
            return;
        }

        // The second headless mode: the documentation CI publishes needs no window either, so it runs before Avalonia
        // starts for the same reason the self check does.
        if (DocsExport.IsRequested(args))
        {
            Environment.ExitCode = DocsExport.Run(args);
            return;
        }

        // The third: a merge driver calls this from a hook, where there is no window to show anything in.
        if (MergeExport.IsRequested(args))
        {
            Environment.ExitCode = MergeExport.Run(args);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}