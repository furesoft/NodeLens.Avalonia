using System.Text;

namespace Projectional.Workbench;

/// <summary>What the screen should show when the user types into a program that cannot echo for itself.</summary>
/// <remarks>
/// <para>
/// This terminal runs a child process with its standard streams redirected and no pseudo-terminal, so nothing echoes:
/// echo is the terminal's job, and a redirected pipe has none. Without this the panel would look broken the moment
/// somebody typed - the keys would reach the program and nothing would appear.
/// </para>
/// <para>
/// So the panel writes the visible part of what it sent, and only the visible part: a cursor-key sequence must not
/// print as <c>^[[A</c>, which is what echoing the bytes would do. A real pseudo-terminal makes this type unnecessary,
/// and the day one is added it is deleted rather than switched off.
/// </para>
/// </remarks>
internal static class TerminalEcho
{
    /// <summary>The bytes to write to the screen for keys the user typed.</summary>
    /// <param name="sent">What was sent to the program.</param>
    internal static byte[] For(ReadOnlySpan<byte> sent)
    {
        if (sent.IsEmpty)
        {
            return [];
        }

        // A named key sends an escape sequence, and a sequence is an instruction rather than a character.
        if (sent[0] == 0x1B)
        {
            return [];
        }

        var buffer = new List<byte>(sent.Length + 2);

        foreach (var value in sent)
        {
            switch (value)
            {
                // Both line endings arrive here: Enter sends a newline, and the screen advances with a carriage
                // return as well or the next line would start where this one ended.
                case (byte)'\r' or (byte)'\n':
                    buffer.Add((byte)'\r');
                    buffer.Add((byte)'\n');
                    break;

                // Delete sends DEL, which a screen erases with by stepping back over a blank.
                case (byte)'\b' or 0x7F:
                    buffer.AddRange([(byte)'\b', (byte)' ', (byte)'\b']);
                    break;

                // A control character is not a character on screen: Ctrl+C is an instruction to the program.
                case < 0x20:
                    break;

                default:
                    buffer.Add(value);
                    break;
            }
        }

        return [.. buffer];
    }

    /// <summary>The bytes to write to the screen for text the user typed.</summary>
    /// <param name="text">What was sent to the program.</param>
    internal static byte[] For(string text) => For(Encoding.UTF8.GetBytes(text));
}
