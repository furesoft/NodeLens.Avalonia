using Avalonia;

namespace Projectional.Screenshots;

/// <summary>
/// Regenerates the screenshots the README shows, one image per editor feature.
/// </summary>
/// <remarks>
/// The images are produced from the shipping control rather than drawn by hand, so a documentation
/// picture cannot show a state the editor no longer reaches. Pass the directory to write into as the
/// first argument; <c>docs/images</c> is the default when the tool is run from the repository root.
/// </remarks>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var output = args.Length > 0 ? args[0] : Path.Combine("docs", "images");

        Directory.CreateDirectory(output);

        return AppBuilder.Configure(() => new ScreenshotApp(output))
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
