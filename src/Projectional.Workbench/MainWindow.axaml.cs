using Avalonia.Controls;
using Avalonia.Threading;

using Projectional.Avalonia;

namespace Projectional.Workbench;

/// <summary>The desktop head: a window whose content is the shared <see cref="WorkbenchView"/>.</summary>
/// <remarks>
/// <para>
/// Everything the shell does lives in the view, because the browser cannot show a <c>Window</c> - Avalonia's browser
/// backend has a single view and no window implementation. What is left here is the part only a desktop window can
/// do: a caption, a close gesture that can be interrupted, and disposing the shell when the window is gone.
/// </para>
/// <para>
/// The standalone app is this window with the default <see cref="ShellOptions"/>; a host that wants a different
/// arrangement composes <see cref="WorkbenchView"/> itself.
/// </para>
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the standalone window: every module, and the desktop's own storage and dialogs.</summary>
    public MainWindow() : this(new ShellOptions())
    {
    }

    /// <summary>Creates the window over <paramref name="options"/>, which is what a host decides.</summary>
    /// <param name="options">The modules the shell may offer and where it keeps what it persists.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public MainWindow(ShellOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        InitializeComponent();

        Shell = new WorkbenchView(options);
        Content = Shell;
        Title = Shell.ShellTitle;

        Shell.TitleChanged += (_, _) => Title = Shell.ShellTitle;

        // The shell asks; the window is the only thing that can close. In the browser nothing answers, because a
        // tab is closed by the browser rather than by the shell.
        Shell.CloseRequested += (_, _) => Close();
    }

    /// <summary>The shell this window hosts, which is what a harness or an embedding host drives.</summary>
    public WorkbenchView Shell { get; }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!Shell.NeedsCloseConfirmation)
        {
            return;
        }

        // Closing is not something a prompt can interrupt: the window is already on its way out, so the close is
        // cancelled and re-issued once every dirty document has been dealt with.
        e.Cancel = true;
        Dispatcher.UIThread.Post(Shell.ConfirmClose);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        Shell.Dispose();

        base.OnClosed(e);
    }
}
