using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Browser;

using Projectional.Avalonia;
using Projectional.Workbench;

namespace Projectional.Browser;

/// <summary>The page's entry point: Avalonia's browser backend with the workbench shell in its single view.</summary>
/// <remarks>
/// A browser has no window and no desktop lifetime, so the shell is hosted as one control
/// (<c>App.SingleViewOptions</c>). Three things differ from the desktop and are stated here rather than in the shell:
/// the terminal needs a child process the browser cannot start, the shell's small state lives in localStorage, and a
/// saved file is downloaded because there is no disk.
/// </remarks>
internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        // Relative to `_framework/`, where the runtime resolves a module import from - which is why the file is one
        // folder up, and why the same page works from a sub-folder such as the documentation's /editor/.
        await JSHost.ImportAsync("shell", "../js/shell.js");

        ExampleLanguages.Extract();

        App.SingleViewOptions = () => new ShellOptions
        {
            Capabilities = ShellCapabilities.Standalone & ~ShellCapability.Terminal,
            Storage = new BrowserShellStorage(),
            Dialogs = new BrowserShellDialogs(),
        };

        App.SingleViewCreated = shell => shell.FileSaved += (_, path) => BrowserFiles.Download(path);

        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();
}
