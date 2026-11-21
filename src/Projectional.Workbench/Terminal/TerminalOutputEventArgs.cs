namespace Projectional.Workbench;

/// <summary>Bytes a running program wrote, in the split they arrived.</summary>
/// <remarks>
/// A split is not a line: a program that writes half an escape sequence and then the rest is writing one thing, and
/// ending the event at a line break would be this type deciding what the program meant.
/// </remarks>
/// <param name="Bytes">The bytes, which the terminal's parser consumes as a stream.</param>
internal sealed class TerminalOutputEventArgs(byte[] bytes) : EventArgs
{
    /// <summary>The bytes the program wrote.</summary>
    internal byte[] Bytes { get; } = bytes;
}
