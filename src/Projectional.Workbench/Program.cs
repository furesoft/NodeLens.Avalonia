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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}