using System.Text;

namespace Projectional.Core;

/// <summary>
/// One case convention: how a name is cut into words and joined together again.
/// </summary>
/// <remarks>
/// <para>
/// Every case rule is this one algorithm with three settings - the separator between words, the casing of the
/// words, and the casing of the first one - which is why <c>camelCase</c> and <c>PascalCase</c> differ in a single
/// value. <see cref="Format"/> is therefore both the repair and the test: a name is valid under a convention exactly
/// when formatting it changes nothing, so a rule cannot accept something its own repair would rewrite.
/// </para>
/// <para>
/// <b>Cutting is the part with decisions.</b> A separator is any character that is neither an ASCII letter nor an
/// ASCII digit, so <c>my rule</c>, <c>my-rule</c> and <c>my_rule</c> all carry the two words <c>my</c> and
/// <c>rule</c>, and the convention's own separator is what appears between them afterwards. Inside a run of
/// letters a word also starts at a hump: an upper-case letter after a lower-case letter (<c>myRule</c>) or, after a
/// capital or a digit, when a lower-case letter follows - which keeps an acronym together (<c>HTTPServer</c> is
/// <c>HTTP</c> and <c>Server</c>) and keeps an all-upper word stable (<c>2ND</c> is one word, so respelling it
/// again inserts no separator). Digits belong to the word they follow, so <c>rule2</c> stays one word.
/// </para>
/// <para>
/// A leading run of underscores is kept, because it is a spelling some languages use for private members
/// (<c>_myField</c>) and dropping it would rename a node rather than repair its spelling. A word that begins with a
/// digit gains one underscore in front, because a name still has to be an identifier - and only when no underscore
/// is already there, which is what keeps the repair idempotent.
/// </para>
/// </remarks>
internal sealed class NameCase
{
    private readonly string _separator;
    private readonly NameWordCase _words;
    private readonly NameWordCase _firstWord;

    private NameCase(string separator, NameWordCase words, NameWordCase firstWord)
    {
        _separator = separator;
        _words = words;
        _firstWord = firstWord;
    }

    /// <summary>Words capitalized and run together: <c>MyRule</c>.</summary>
    public static NameCase Pascal { get; } =
        new(string.Empty, NameWordCase.Capitalized, NameWordCase.Capitalized);

    /// <summary>The first word lower and the rest capitalized, run together: <c>myRule</c>.</summary>
    public static NameCase Camel { get; } =
        new(string.Empty, NameWordCase.Capitalized, NameWordCase.Lower);

    /// <summary>Lower-case words joined by underscores: <c>my_rule</c>.</summary>
    public static NameCase Snake { get; } = new("_", NameWordCase.Lower, NameWordCase.Lower);

    /// <summary>Upper-case words joined by underscores: <c>MY_RULE</c>.</summary>
    public static NameCase ScreamingSnake { get; } = new("_", NameWordCase.Upper, NameWordCase.Upper);

    /// <summary>Lower-case words joined by hyphens: <c>my-rule</c>.</summary>
    public static NameCase Kebab { get; } = new("-", NameWordCase.Lower, NameWordCase.Lower);

    /// <summary>Upper-case words joined by hyphens: <c>MY-RULE</c>.</summary>
    public static NameCase ScreamingKebab { get; } = new("-", NameWordCase.Upper, NameWordCase.Upper);

    /// <summary>Capitalized words joined by hyphens: <c>My-Rule</c>.</summary>
    public static NameCase Train { get; } = new("-", NameWordCase.Capitalized, NameWordCase.Capitalized);

    /// <summary>Lower-case words joined by dots: <c>my.rule</c>.</summary>
    public static NameCase Dot { get; } = new(".", NameWordCase.Lower, NameWordCase.Lower);

    /// <summary>Capitalized words joined by dots: <c>My.Rule</c>, the spelling of a concept key.</summary>
    public static NameCase PascalDot { get; } =
        new(".", NameWordCase.Capitalized, NameWordCase.Capitalized);

    /// <summary>Whether <paramref name="name"/> is already spelled the way this convention says.</summary>
    /// <remarks>
    /// Formatting is the whole test on purpose: a second implementation of "is this camel case" would be a second
    /// answer to the same question, and the one the repair uses is the one a user sees.
    /// </remarks>
    public bool Matches(string name) => string.Equals(Format(name), name, StringComparison.Ordinal);

    /// <summary><paramref name="name"/> respelled under this convention.</summary>
    /// <remarks>
    /// Mechanical, like the identifier repair: it cuts the name into the words it already has and only changes how
    /// they are joined, so a reader can see what changed and nothing is invented. A name that carries no word at
    /// all (only separators) becomes underscores of its own length rather than an empty name.
    /// </remarks>
    public string Format(string name)
    {
        var words = Split(name, out var underscores);

        if (words.Count == 0)
        {
            return new string('_', name.Length);
        }

        var builder = new StringBuilder(name.Length + 1);
        builder.Append('_', underscores);

        var body = new StringBuilder(name.Length);
        for (var index = 0; index < words.Count; index++)
        {
            if (index > 0)
            {
                body.Append(_separator);
            }

            body.Append(CaseOf(words[index], index == 0 ? _firstWord : _words));
        }

        // A name may not begin with a digit. An underscore in front is the one spelling that keeps the word
        // readable, and the guard on the prefix is what makes the repair idempotent: `_2nd` already carries one.
        if (underscores == 0 && char.IsAsciiDigit(body[0]))
        {
            builder.Append('_');
        }

        return builder.Append(body).ToString();
    }

    private static string CaseOf(string word, NameWordCase wordCase) => wordCase switch
    {
        NameWordCase.Upper => word.ToUpperInvariant(),
        NameWordCase.Capitalized => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant(),
        _ => word.ToLowerInvariant(),
    };

    /// <summary>The words of <paramref name="name"/>, and how many underscores stand in front of them.</summary>
    private static List<string> Split(string name, out int underscores)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var index = 0;

        while (index < name.Length && name[index] == '_')
        {
            index++;
        }

        underscores = index;

        for (; index < name.Length; index++)
        {
            var character = name[index];

            if (!char.IsAsciiLetterOrDigit(character))
            {
                Take(words, current);
                continue;
            }

            if (current.Length > 0 && char.IsAsciiLetterUpper(character) && StartsWord(name, index))
            {
                Take(words, current);
            }

            current.Append(character);
        }

        Take(words, current);

        return words;
    }

    /// <summary>Whether the upper-case letter at <paramref name="index"/> opens a word rather than continuing one.</summary>
    /// <remarks>
    /// <para>
    /// A hump opens after a lower-case letter (<c>myRule</c>) and, after an upper-case letter or a digit, when a
    /// lower-case letter follows (<c>HTTPServer</c>, <c>rule2Go</c>). The second half is what keeps an acronym
    /// together, and treating a digit like a capital is what makes the repair idempotent: without it <c>2ND</c>
    /// would split into <c>2</c> and <c>ND</c>, so respelling an already-upper word would keep inserting separators.
    /// </para>
    /// </remarks>
    private static bool StartsWord(string name, int index) =>
        char.IsAsciiLetterLower(name[index - 1])
        || (index + 1 < name.Length && char.IsAsciiLetterLower(name[index + 1]));

    private static void Take(List<string> words, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        words.Add(current.ToString());
        current.Clear();
    }
}