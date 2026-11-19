using YamlDotNet.Core;
using YamlDotNet.Core.Tokens;

namespace Projectional.Yaml;

/// <summary>The token stream of a YAML file, walked forwards once.</summary>
/// <remarks>
/// <para>
/// <see cref="Scanner"/> is the tokenizer, taken with <c>skipComments: false</c> so notes are visible: the parser above it
/// is what drops them, and a dropped note is a rewritten file (invariant 8). The scanner resolves indentation into
/// explicit block tokens, which is the part of YAML that is genuinely hard - this importer therefore owns the mapping and
/// not the layout.
/// </para>
/// <para>
/// Every token keeps its offsets, so a scalar's text is a slice of the file rather than a re-spelling, and a construct
/// this language does not model can be kept as one raw slice.
/// </para>
/// </remarks>
internal sealed class YamlTokens
{
    private readonly List<YamlToken> _tokens;
    private readonly List<YamlToken> _held = [];
    private int _index;

    private YamlTokens(List<YamlToken> tokens, string text)
    {
        _tokens = tokens;
        Text = text;
    }

    /// <summary>The text the tokens came from, so a raw construct can be sliced out of it.</summary>
    internal string Text { get; }

    /// <summary>Whether the reader has walked past the last token.</summary>
    internal bool AtEnd => _index >= _tokens.Count;

    /// <summary>The token the reader stands on.</summary>
    internal YamlToken Current => _index < _tokens.Count ? _tokens[_index] : new YamlToken(YamlTokenKind.Other, string.Empty, Text.Length, Text.Length);

    /// <summary>Steps past the token the reader stands on.</summary>
    internal void Advance()
    {
        if (_index < _tokens.Count)
        {
            LastEnd = _tokens[_index].End;
        }

        _index++;
    }

    /// <summary>Where the token that was last stepped past ended.</summary>
    internal int LastEnd { get; private set; }

    /// <summary>Keeps a note for the list that is about to be created.</summary>
    /// <param name="note">The note to hold.</param>
    /// <remarks>
    /// A note may stand between a pair's colon and the collection that follows it, and it belongs to that collection's
    /// list - which does not exist yet when the note is read. Holding it until a list starts is what keeps the note where
    /// the file had it, instead of moving it out to the surrounding list.
    /// </remarks>
    internal void Hold(YamlToken note) => _held.Add(note);

    /// <summary>The notes held for a list that is starting, which are now its first members.</summary>
    internal IReadOnlyList<YamlToken> TakeHeld()
    {
        if (_held.Count == 0)
        {
            return [];
        }

        YamlToken[] held = [.. _held];
        _held.Clear();

        return held;
    }

    /// <summary>The slice of the file a token spans, without the line break that ends its line.</summary>
    internal string Slice(YamlToken token) => Spelling(token.Start, token.End);

    /// <summary>A slice of the file, without the one line break that ends its last line.</summary>    /// <param name="start">Where the slice starts, as a character offset.</param>
    /// <param name="end">Where it ends, exclusive.</param>
    /// <remarks>
    /// A block scalar and a block collection are the two spellings whose token runs to the end of a line, and YAML ends
    /// that line with a break. The break belongs to the file's line structure, which the model's separators write, so a
    /// spelling that kept it would print the member after it one line too low.
    /// </remarks>
    internal string Spelling(int start, int end)
    {
        var slice = Text[start..Math.Clamp(end, start, Text.Length)];

        return slice.EndsWith('\n') ? slice[..^1] : slice;
    }

    /// <summary>Whether the reader stands on a token that can begin a node.</summary>
    internal bool AtNodeStart() => !AtEnd && Current.Kind is
        YamlTokenKind.Scalar or
        YamlTokenKind.Alias or
        YamlTokenKind.Anchor or
        YamlTokenKind.Tag or
        YamlTokenKind.MappingStart or
        YamlTokenKind.SequenceStart or
        YamlTokenKind.FlowMappingStart or
        YamlTokenKind.FlowSequenceStart;

    /// <summary>Tokenizes <paramref name="text"/>, which must already use LF line endings.</summary>
    /// <param name="text">The whole text a host passed.</param>
    /// <exception cref="YamlException">The text is not YAML at all.</exception>
    internal static YamlTokens Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var scanner = new Scanner(new StringReader(text), skipComments: false);
        List<YamlToken> tokens = [];

        while (scanner.MoveNext())
        {
            // `MoveNext` just succeeded, so a current token exists; `Current` is nullable because it answers null before the
            // first step.
            var token = scanner.Current!;
            var (kind, spelled) = token switch
            {
                BlockMappingStart => (YamlTokenKind.MappingStart, string.Empty),
                FlowMappingStart => (YamlTokenKind.FlowMappingStart, string.Empty),
                BlockSequenceStart => (YamlTokenKind.SequenceStart, string.Empty),
                FlowSequenceStart => (YamlTokenKind.FlowSequenceStart, string.Empty),
                BlockEnd => (YamlTokenKind.BlockEnd, string.Empty),
                FlowMappingEnd => (YamlTokenKind.FlowMappingEnd, string.Empty),
                FlowSequenceEnd => (YamlTokenKind.FlowSequenceEnd, string.Empty),
                Key => (YamlTokenKind.Key, string.Empty),
                Value => (YamlTokenKind.Value, ":"),
                FlowEntry => (YamlTokenKind.FlowEntry, ","),
                BlockEntry => (YamlTokenKind.Entry, "-"),
                YamlDotNet.Core.Tokens.Scalar scalar => (YamlTokenKind.Scalar, scalar.Value ?? string.Empty),
                AnchorAlias alias => (YamlTokenKind.Alias, Name(alias.Value)),
                Anchor anchor => (YamlTokenKind.Anchor, Name(anchor.Value)),
                Tag => (YamlTokenKind.Tag, string.Empty),
                VersionDirective version =>
                    (YamlTokenKind.Other, string.Create(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"%YAML {version.Version.Major}.{version.Version.Minor}")),
                TagDirective directive => (YamlTokenKind.Other, $"%TAG {Name(directive.Handle)} {Name(directive.Prefix)}"),
                Comment => (YamlTokenKind.Comment, string.Empty),
                _ => (YamlTokenKind.Other, string.Empty),
            };

            tokens.Add(new YamlToken(kind, spelled, (int)token.Start.Index, (int)token.End.Index));
        }

        return new YamlTokens(tokens, text);
    }

    /// <summary>An anchor's name, or nothing when the token carries no name.</summary>
    private static string Name(AnchorName name) => name.IsEmpty ? string.Empty : name.Value ?? string.Empty;
}
