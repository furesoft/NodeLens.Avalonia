using System.Diagnostics;

namespace Projectional.Workbench;

/// <summary>A program the terminal runs: its output as bytes, its input as bytes.</summary>
/// <remarks>
/// <para>
/// The three streams are redirected, which is the simplest thing that works and the honest limit of this panel: with
/// no pseudo-terminal a child does not echo, does not do line editing, and does not colour its output unless it is
/// asked to. That is enough for what the panel is for - watching what a program prints and typing answers into it -
/// and it is why <see cref="TerminalEcho"/> exists. A pseudo-terminal is a separate decision with a native dependency
/// per platform, and it is recorded as such rather than half-built here.
/// </para>
/// <para>
/// Output is read as bytes and never as lines: a terminal's input is a character stream, and a reader that split it
/// into lines would hand the emulator escape sequences it can no longer recognise.
/// </para>
/// </remarks>
internal sealed class TerminalProcess : IDisposable
{
    private readonly Process _process;
    private readonly CancellationTokenSource _stop = new();

    private bool _disposed;

    private TerminalProcess(Process process) => _process = process;

    /// <summary>Starts pumping both output streams, which only works once the process is running.</summary>
    /// <remarks>
    /// Called by <see cref="Start"/> after the process started, and never by the constructor: a redirected stream does
    /// not exist until then, and reading one asks the process for it - which answers "StandardOut has not been
    /// redirected or the process hasn't been started yet" instead of blocking.
    /// </remarks>
    private void StartReading()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _ = ReadAsync(_process.StandardOutput.BaseStream);
        _ = ReadAsync(_process.StandardError.BaseStream);
    }

    /// <summary>Raised for each read of the program's output and its error stream.</summary>
    internal event EventHandler<TerminalOutputEventArgs>? Output;

    /// <summary>Raised once, when the program ends.</summary>
    /// <remarks>
    /// The exit code is part of the sentence the panel writes into the screen, because "nothing else is coming" and
    /// "it failed" are two different things to a user watching a test run.
    /// </remarks>
    internal event EventHandler<TerminalExitEventArgs>? Exited;

    /// <summary>Starts <paramref name="command"/> with the given streams redirected.</summary>
    /// <param name="command">The program and its arguments.</param>
    /// <param name="output">Where what the program prints goes.</param>
    /// <param name="exited">Where the news that it ended goes.</param>
    /// <param name="workingDirectory">Where it starts, or <see langword="null"/> for the shell's own directory.</param>
    /// <remarks>
    /// The handlers are given here rather than attached afterwards because reading begins inside this method: a caller
    /// that subscribed once it had the process back would lose whatever the program printed in the meantime, which for
    /// a short-lived program (<c>dotnet --version</c>) is everything it printed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The program could not be started.</exception>
    internal static TerminalProcess Start(
        TerminalCommand command,
        EventHandler<TerminalOutputEventArgs> output,
        EventHandler<TerminalExitEventArgs> exited,
        string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(exited);

        if (OperatingSystem.IsBrowser())
        {
            // The browser has no child processes at all. The shell does not offer the terminal there, so this is the
            // message a caller that ignores the capability set gets rather than a PlatformNotSupportedException from
            // somewhere inside the runtime.
            throw new PlatformNotSupportedException("a browser cannot start a program, so the terminal has no session.");
        }

        var info = new ProcessStartInfo(command.FileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        foreach (var argument in command.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        var started = new TerminalProcess(process);

        started.Output += output;
        started.Exited += exited;

        process.Exited += (_, _) => started.ReportExit();

        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            started.Dispose();

            throw new InvalidOperationException(
                $"'{command.FileName}' could not be started: {exception.Message}",
                exception);
        }

        // Only now are the streams there to read.
        started.StartReading();

        return started;
    }

    /// <summary>Whether the program is still running.</summary>
    internal bool IsRunning => !OperatingSystem.IsBrowser() && !_process.HasExited;

    /// <summary>Sends bytes to the program's standard input.</summary>
    /// <param name="bytes">The bytes, which are what the keys the user pressed mean.</param>
    /// <remarks>
    /// A program that has already ended has a closed pipe, and a write to it throws on some platforms and is ignored
    /// on others. Neither is worth reporting: the exit was reported as it happened.
    /// </remarks>
    internal void Write(ReadOnlySpan<byte> bytes)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        if (bytes.IsEmpty || _process.HasExited)
        {
            return;
        }

        try
        {
            _process.StandardInput.BaseStream.Write(bytes);
            _process.StandardInput.BaseStream.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The program closed its input or ended while the write was in flight.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        _disposed = true;

        _stop.Cancel();

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            // It ended between the check and the kill, which is the outcome the kill was for.
        }

        _process.Dispose();
        _stop.Dispose();
    }

    /// <summary>Raises <see cref="Exited"/> with the code the process answered with.</summary>
    /// <remarks>
    /// Every read of the process is guarded, because this runs on a thread of the platform's choosing and it can arrive
    /// after the panel let the process go: an exception escaping here is not a failed terminal, it takes the
    /// application down - which is exactly what happened the first time a program was started and the window closed
    /// before its exit arrived.
    /// </remarks>
    private void ReportExit()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        if (_disposed)
        {
            return;
        }

        var code = -1;

        try
        {
            code = _process.HasExited ? _process.ExitCode : -1;
        }
        catch (InvalidOperationException)
        {
            // The handle is already gone, so the code is unknown and the sentence says so.
        }

        Exited?.Invoke(this, new TerminalExitEventArgs(code));
    }

    /// <summary>Pumps one of the program's output streams into <see cref="Output"/> until it ends.</summary>
    /// <param name="stream">The stream to read.</param>
    private async Task ReadAsync(Stream stream)
    {
        var buffer = new byte[4096];

        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, _stop.Token).ConfigureAwait(false);

                if (read <= 0)
                {
                    return;
                }

                Output?.Invoke(this, new TerminalOutputEventArgs(buffer[..read]));
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or IOException or InvalidOperationException)
        {
            // Disposed, or the child closed the pipe: either way nothing more is coming.
        }
    }
}
