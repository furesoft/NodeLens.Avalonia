namespace Projectional.Hocon;

/// <summary>The token stream of a HOCON file, walked forwards once.</summary>
/// <remarks>
/// No lookahead beyond one token, because HOCON's grammar needs none: a newline ends a member, a comma is optional, and a
/// value continues only while another value starts on the same line. That is the whole reason a hand-written parser is
/// tractable here, and it is what keeps a concatenation from swallowing the next key.
/// </remarks>
internal sealed class HoconTokens
{
    private readonly IReadOnlyList<HoconToken> _tokens;
    private int _index;

    internal HoconTokens(IReadOnlyList<HoconToken> tokens) => _tokens = tokens;

    /// <summary>Whether the reader has walked past the last token.</summary>
    internal bool AtEnd => _index >= _tokens.Count || _tokens[_index].Kind == HoconTokenKind.End;

    /// <summary>The token the reader stands on.</summary>
    internal HoconToken Current =>
        _index < _tokens.Count ? _tokens[_index] : new HoconToken(HoconTokenKind.End, string.Empty, 0);

    /// <summary>Steps past the token the reader stands on.</summary>
    internal void Advance() => _index++;

    /// <summary>Steps over the line breaks, which separate members and items.</summary>
    internal void SkipNewLines()
    {
        while (!AtEnd && Current.Kind == HoconTokenKind.NewLine)
        {
            _index++;
        }
    }

    /// <summary>Whether the reader stands on a token that can begin a value.</summary>
    internal bool AtValueStart() => !AtEnd && Current.Kind is
        HoconTokenKind.OpenBrace or
        HoconTokenKind.OpenBracket or
        HoconTokenKind.Substitution or
        HoconTokenKind.Quoted or
        HoconTokenKind.TripleQuoted or
        HoconTokenKind.Bare;
}
