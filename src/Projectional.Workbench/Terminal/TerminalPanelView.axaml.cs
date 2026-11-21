using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Projectional.Workbench;

/// <summary>The terminal tool: a command box, its output, and the keys typed back into it.</summary>
/// <remarks>
/// <para>
/// The panel is what connects the three halves - a <see cref="TerminalSession"/> that emulates, a
/// <see cref="TerminalProcess"/> that runs, and a <see cref="TerminalView"/> that paints - and it owns the one policy
/// that is neither of them: <b>local echo</b>. A redirected child cannot echo for itself, so the panel writes the
/// visible part of what it sent (<see cref="TerminalEcho"/>) and then sends the bytes on.
/// </para>
/// <para>
/// Output arrives on a reader's thread and is marshalled to the UI thread before it reaches the session, because the
/// session raises its change event and a control cannot be invalidated from another thread.
/// </para>
/// </remarks>
internal sealed partial class TerminalPanelView : UserControl
{
    private TerminalProcess? _process;

    /// <summary>The terminal tool, with an empty screen.</summary>
    internal TerminalPanelView()
    {
        InitializeComponent();

        Screen.Session = new TerminalSession();
        Screen.Palette = TerminalPalette.Light;
        Screen.Input += OnScreenInput;

        StartButton.Click += OnStart;
        StopButton.Click += OnStop;
    }

    /// <summary>The screen, which is where everything the program wrote ends up.</summary>
    internal TerminalView View => Screen;

    /// <summary>The session the screen paints, which is also what a test can read.</summary>
    internal TerminalSession Session => Screen.Session!;

    /// <summary>Whether a program is running right now.</summary>
    internal bool IsRunning => _process is { IsRunning: true };

    /// <summary>Starts <paramref name="command"/>, which is the same code the Start button runs.</summary>
    /// <param name="command">A command line, split the way the box's own text is.</param>
    /// <exception cref="InvalidOperationException">The program could not be started.</exception>
    internal void Run(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        CommandBox.Text = command;

        Start();
    }

    /// <summary>Runs what the box says.</summary>
    internal void Start()
    {
        Stop();

        if (!TerminalCommandLine.TrySplit(CommandBox.Text ?? string.Empty, out var command))
        {
            return;
        }

        Session.Write(command.Arguments.Count == 0
            ? $"{command.FileName}\r\n"
            : $"{command.FileName} {string.Join(' ', command.Arguments)}\r\n");

        try
        {
            _process = TerminalProcess.Start(command, OnProcessOutput, OnProcessExited);
        }
        catch (InvalidOperationException exception)
        {
            Session.Write($"{exception.Message}\r\n");

            return;
        }

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;

        Screen.Focus();
    }

    /// <summary>Stops the program, if one is running.</summary>
    internal void Stop()
    {
        if (_process is { } process)
        {
            _process = null;

            process.Dispose();
        }

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    /// <summary>Sends a key the screen produced to the program, echoed so that the user sees what they typed.</summary>
    private void OnScreenInput(object? sender, TerminalOutputEventArgs e)
    {
        Session.Write(TerminalEcho.For(e.Bytes));

        _process?.Write(e.Bytes);
    }

    /// <summary>Writes what the program printed, on the UI thread.</summary>
    private void OnProcessOutput(object? sender, TerminalOutputEventArgs e) =>
        Dispatcher.UIThread.Post(() => Session.Write(e.Bytes));

    /// <summary>Says how the program ended, in the screen, so the last line is not a silence.</summary>
    /// <remarks>
    /// The whole of it is marshalled, not just the write: the platform raises its exit event on a thread of its own, and
    /// the button states this sets belong to the UI thread - which is how the first version of this panel took the
    /// application down the moment a short-lived program finished.
    /// </remarks>
    private void OnProcessExited(object? sender, TerminalExitEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            Stop();

            // A program that ended its output with a newline already left the cursor at the start of a line, so the
            // notice continues there rather than opening a gap that looks like a line the program printed.
            var notice = $"[exited with code {e.ExitCode}]\r\n";

            Session.Write(Session.Cursor.Column == 0 ? notice : $"\r\n{notice}");
        });

    private void OnStart(object? sender, RoutedEventArgs e) => Start();

    private void OnStop(object? sender, RoutedEventArgs e) => Stop();
}
