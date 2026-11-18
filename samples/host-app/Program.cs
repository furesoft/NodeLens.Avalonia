using Avalonia;

namespace HostApp;

/// <summary>
/// A host application that embeds the projectional editor: one document of a shipped language, the control that
/// draws it, and the three things a shell does with it - show what is wrong, run it, generate its text.
/// </summary>
/// <remarks>
/// Nothing here reaches past the published surface, and that is the whole point of the sample: it is what makes the
/// M4 gate's "inside the embedded control in a small host application" a fact rather than a claim. The standalone
/// editor is not referenced; every call below is one a third party could make.
/// </remarks>
internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>The builder Avalonia's own tooling looks for, kept public for that reason.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HostApplication>()
            .UsePlatformDetect()
            .LogToTrace();
}
