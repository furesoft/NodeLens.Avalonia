using System.Text;

namespace Projectional.Workbench;

/// <summary>A command line split into the program to run and the arguments it is given.</summary>
/// <param name="FileName">The program, as the platform's process starter understands it.</param>
/// <param name="Arguments">The arguments, already separate - so an argument containing a space stays one argument.</param>
internal sealed record TerminalCommand(string FileName, IReadOnlyList<string> Arguments);

/// <summary>The little grammar a command typed into the terminal's box is read with.</summary>
/// <remarks>
/// <para>
/// The process starter takes a program and a list of arguments, so a line typed into one box has to be split again -
/// and splitting on spaces is wrong the moment an argument is a path with a space in it. Double quotes are the one
/// thing this grammar knows, which is the convention every shell on both platforms shares; backslash escapes are
/// deliberately absent, because Windows and Unix disagree about them and a half-implemented escape rule is worse than
/// none.
/// </para>
/// <para>
/// An empty line, or one that is only blanks, has no program: <see cref="TrySplit"/> answers no rather than starting
/// something arbitrary.
/// </para>
/// </remarks>
internal static class TerminalCommandLine
{
    /// <summary>Reads <paramref name="line"/> into the program and its arguments.</summary>
    /// <param name="line">What the user typed.</param>
    /// <param name="command">The command, when the line names a program.</param>
    internal static bool TrySplit(string line, out TerminalCommand command)
    {
        ArgumentNullException.ThrowIfNull(line);

        var words = Words(line);

        command = words.Count == 0
            ? new TerminalCommand(string.Empty, [])
            : new TerminalCommand(words[0], [.. words.Skip(1)]);

        return words.Count > 0;
    }

    /// <summary>The words of <paramref name="line"/>, with quoted runs kept whole.</summary>
    /// <param name="line">The line to read.</param>
    private static List<string> Words(string line)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var quoted = false;
        var started = false;

        foreach (var character in line)
        {
            if (character == '"')
            {
                // A quote opens or closes a run, and a run is a word even when it is empty - which is what makes
                // `prog ""` two words rather than one.
                quoted = !quoted;
                started = true;
                continue;
            }

            if (!quoted && char.IsWhiteSpace(character))
            {
                if (started)
                {
                    words.Add(word.ToString());
                    word.Clear();
                    started = false;
                }

                continue;
            }

            word.Append(character);
            started = true;
        }

        if (started)
        {
            words.Add(word.ToString());
        }

        return words;
    }
}
