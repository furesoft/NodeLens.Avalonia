namespace Projectional.Hocon;

/// <summary>The kinds of token a HOCON file is made of.</summary>
/// <remarks>
/// `#` and `//` are comments; `{ } [ ] , : = +=` are the punctuation; a substitution is one token because nothing inside
/// it is HOCON; and everything else is a bare run, which the parser classifies as a number, a literal or an unquoted
/// string. There is no separate keyword token: `true`, `false`, `null` and `include` are bare runs whose meaning is the
/// parser's, which is the same reason <c>token</c> has no "number" kind of its own.
/// </remarks>
internal enum HoconTokenKind
{
    /// <summary>The end of the text.</summary>
    End,

    /// <summary>A line break, which ends a member where no comma does.</summary>
    NewLine,

    /// <summary>A note, kept with the marker that introduced it.</summary>
    Comment,

    /// <summary>An opening brace.</summary>
    OpenBrace,

    /// <summary>A closing brace.</summary>
    CloseBrace,

    /// <summary>An opening bracket.</summary>
    OpenBracket,

    /// <summary>A closing bracket.</summary>
    CloseBracket,

    /// <summary>A comma, which is optional in this format.</summary>
    Comma,

    /// <summary>A colon: one of the four separators.</summary>
    Colon,

    /// <summary>An equals sign: one of the four separators.</summary>
    Equals,

    /// <summary>An appending assignment: one of the four separators.</summary>
    PlusEquals,

    /// <summary>A whole substitution, <c>${a.b}</c> or <c>${?a.b}</c>, kept as written.</summary>
    Substitution,

    /// <summary>A string between one pair of double quotes, escapes included.</summary>
    Quoted,

    /// <summary>A string between three pairs of double quotes, which may span lines.</summary>
    TripleQuoted,

    /// <summary>An unquoted run: a key, a number, a literal or a string.</summary>
    Bare,
}
