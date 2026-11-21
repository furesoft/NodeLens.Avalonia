namespace Projectional.Workbench;

/// <summary>How a program the terminal ran ended.</summary>
/// <param name="ExitCode">The code it answered with, or <c>-1</c> when the platform would not say.</param>
internal sealed class TerminalExitEventArgs(int exitCode) : EventArgs
{
    /// <summary>The exit code of the program.</summary>
    internal int ExitCode { get; } = exitCode;
}
