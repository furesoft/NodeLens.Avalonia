using Tomlyn.Syntax;

namespace Projectional.Toml;

/// <summary>The TOML reader's token stream, walked forwards once.</summary>
/// <remarks>
/// <para>
/// The lexer is Tomlyn's, because number grammar, the four string kinds, the four date and time kinds and the escape
/// rules of a basic string are exactly the part of a file format that a hand-written lexer gets wrong. The tokens are
/// re-read here rather than walked through Tomlyn's syntax tree because a tree node does not carry its place in the
/// text: this parser has to know where a token is, and it has to see comments, which the tree keeps only sometimes and
/// attaches to whichever node happened to follow.
/// </para>
/// <para>
/// The whole stream is materialised up front, because the concatenation of the texts is the file byte for byte - which
/// is what lets a comment be placed where it was written and a diagnostic point at the character that broke it.
/// </para>
/// </remarks>
internal sealed class TomlTokens
{
    private readonly List<TomlToken> _tokens;
    private int _index;

    private TomlTokens(List<TomlToken> tokens) => _tokens = tokens;

    /// <summary>Whether the reader has walked past the last token.</summary>
    internal bool AtEnd => _index >= _tokens.Count;

    /// <summary>The token the reader stands on.</summary>
    internal TomlToken Current =>
        _index < _tokens.Count ? _tokens[_index] : throw new InvalidOperationException("The reader is past the end.");

    /// <summary>Wraps a parsed document's token stream.</summary>
    /// <param name="document">The document Tomlyn parsed.</param>
    internal static TomlTokens Of(DocumentSyntax document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<TomlToken> tokens = [];
        var offset = 0;
        foreach (var item in document.Tokens(includeCommentsAndWhitespaces: true))
        {
            var text = item switch
            {
                SyntaxToken token => token.Text ?? string.Empty,
                SyntaxTrivia trivia => trivia.Text ?? string.Empty,
                _ => string.Empty,
            };

            tokens.Add(item switch
            {
                SyntaxToken token => new TomlToken(token.TokenKind, text, offset),
                SyntaxTrivia trivia => new TomlToken(trivia.Kind, text, offset),
                _ => new TomlToken(TokenKind.Invalid, text, offset),
            });

            offset += text.Length;
        }

        return new TomlTokens(tokens);
    }

    /// <summary>Steps past the token the reader stands on.</summary>
    internal void Advance() => _index++;

    /// <summary>Steps over the spaces and tabs, which no rule cares about.</summary>
    internal void SkipWhitespace()
    {
        while (!AtEnd && Current.Kind == TokenKind.Whitespaces)
        {
            _index++;
        }
    }

    /// <summary>Steps over the spaces, the line breaks and the comments between two parts of a value.</summary>
    /// <param name="onComment">What to do with a comment that stands inside the value; it may be
    /// <see langword="null"/> when the caller is somewhere a comment cannot be kept.</param>
    internal void SkipInsignificant(Action<TomlToken>? onComment)
    {
        while (!AtEnd)
        {
            switch (Current.Kind)
            {
                case TokenKind.Whitespaces or TokenKind.NewLine:
                    _index++;
                    continue;
                case TokenKind.Comment:
                    onComment?.Invoke(Current);
                    _index++;
                    continue;
                default:
                    return;
            }
        }
    }

    /// <summary>Takes the token the reader stands on when it is <paramref name="kind"/>.</summary>
    /// <param name="kind">The kind that has to be there.</param>
    internal bool TryTake(TokenKind kind)
    {
        SkipWhitespace();

        if (AtEnd || Current.Kind != kind)
        {
            return false;
        }

        _index++;

        return true;
    }

    /// <summary>The token the reader stands on, which has to be <paramref name="kind"/>.</summary>
    /// <param name="kind">The kind that has to be there.</param>
    /// <exception cref="FormatException">The file does not hold that token here.</exception>
    internal TomlToken Expect(TokenKind kind)
    {
        SkipWhitespace();

        if (AtEnd || Current.Kind != kind)
        {
            throw new FormatException($"Expected {kind} but the file holds {Spelling()}.");
        }

        return _tokens[_index++];
    }

    /// <summary>The token the reader stands on, which has to be one of the kinds that may name a key.</summary>
    /// <exception cref="FormatException">The file does not hold a key part here.</exception>
    internal TomlToken ExpectKeyPart()
    {
        SkipWhitespace();

        if (AtEnd || !IsKeyPart(Current.Kind))
        {
            throw new FormatException($"Expected a key but the file holds {Spelling()}.");
        }

        return _tokens[_index++];
    }

    /// <summary>Whether <paramref name="kind"/> can be part of a key.</summary>
    /// <remarks>
    /// A bare key, a quoted key and - because the lexer cannot know that a number standing where a key belongs is a
    /// key - the literal tokens. A key is kept as the text it was written as, so which of them it was does not matter.
    /// </remarks>
    internal static bool IsKeyPart(TokenKind kind) => kind is
        TokenKind.BasicKey or
        TokenKind.String or
        TokenKind.StringLiteral or
        TokenKind.Integer or
        TokenKind.IntegerHexa or
        TokenKind.IntegerOctal or
        TokenKind.IntegerBinary or
        TokenKind.Float;

    private string Spelling() => AtEnd ? "the end of the file" : $"'{Current.Text}'";
}
