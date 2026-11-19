using System.Text;

namespace Projectional.Xml;

/// <summary>The one place XML's spelling rules live: how a value is written so the file means what it says.</summary>
/// <remarks>
/// <para>
/// Applied when a value enters the model, not when it leaves it. The parser hands back the decoded value, the model
/// holds the canonical spelling, and the generator emits a slot verbatim - which is what keeps the framework's template
/// vocabulary free of a format-specific escaping part. The JSON language does the same thing for the escapes inside a
/// string; here the escape is XML's, and there are two of them because character data and an attribute value are not
/// escaped the same way.
/// </para>
/// <para>
/// Characters are escaped as named entities where XML has one (<c>&amp;amp;</c>, <c>&amp;lt;</c>, <c>&amp;gt;</c>,
/// <c>&amp;quot;</c>) and as a character reference where it does not. <c>&gt;</c> is escaped in text even though XML only
/// requires it inside <c>]]&gt;</c>: a canonical spelling has to pick one form, and this is the one every XML writer picks.
/// A tab, a newline or a carriage return in an attribute value must be a character reference, because the parser
/// normalises the literal ones to a space and the value would change on the way back in.
/// </para>
/// </remarks>
internal static class XmlEscaping
{
    /// <summary>The canonical spelling of character data.</summary>
    /// <param name="value">The decoded value the parser handed back.</param>
    internal static string Text(string value) => Spell(value, inAttribute: false);

    /// <summary>The canonical spelling of an attribute value.</summary>
    /// <param name="value">The decoded value the parser handed back.</param>
    internal static string Attribute(string value) => Spell(value, inAttribute: true);

    private static string Spell(string value, bool inAttribute)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!NeedsEscaping(value, inAttribute))
        {
            return value;
        }

        var spelled = new StringBuilder(value.Length + 8);
        foreach (var character in value)
        {
            switch (character)
            {
                case '&':
                    spelled.Append("&amp;");
                    break;
                case '<':
                    spelled.Append("&lt;");
                    break;
                case '>':
                    spelled.Append("&gt;");
                    break;
                case '"' when inAttribute:
                    spelled.Append("&quot;");
                    break;
                case '\t' when inAttribute:
                    spelled.Append("&#x9;");
                    break;
                case '\n' when inAttribute:
                    spelled.Append("&#xA;");
                    break;
                case '\r' when inAttribute:
                    spelled.Append("&#xD;");
                    break;
                default:
                    spelled.Append(character);
                    break;
            }
        }

        return spelled.ToString();
    }

    private static bool NeedsEscaping(string value, bool inAttribute)
    {
        foreach (var character in value)
        {
            if (character is '&' or '<' or '>' || (inAttribute && character is '"' or '\t' or '\n' or '\r'))
            {
                return true;
            }
        }

        return false;
    }
}
