using System.Text;
using System.Text.Json;

namespace Projectional.Json.Checks;

/// <summary>The JSON spelling rules a check needs: is this text legal, what does it mean, and how is it written.</summary>
/// <remarks>
/// The importer keeps the file's spelling and never decodes it, which is right for round-tripping and useless for a
/// check: a check has to ask whether a value <em>could</em> have come from a file at all. The platform's tokenizer
/// answers that by parsing the single token again, wrapped in the quotes it belongs in, so the rules are not restated
/// here either.
/// </remarks>
internal static class JsonSpelling
{
    /// <summary>Whether <paramref name="spelled"/> is a legal spelling of a JSON string's contents.</summary>
    /// <param name="spelled">The text as it would sit between the quotes.</param>
    internal static bool IsLegalString(string spelled)
    {
        ArgumentNullException.ThrowIfNull(spelled);

        try
        {
            using var document = JsonDocument.Parse($"\"{spelled}\"");
            return document.RootElement.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Whether <paramref name="text"/> is a JSON number.</summary>
    /// <param name="text">The literal a number cell holds.</param>
    internal static bool IsLegalNumber(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Number;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The value <paramref name="spelled"/> stands for, or the text itself when it stands for nothing.</summary>
    /// <param name="spelled">The text as the file spells it.</param>
    /// <remarks>
    /// The fallback is what lets a repair of an unreadable value still propose something: a name that decodes to nothing
    /// is escaped as the literal characters it holds, which is the only reading that keeps them.
    /// </remarks>
    internal static string Decode(string spelled)
    {
        ArgumentNullException.ThrowIfNull(spelled);

        try
        {
            using var document = JsonDocument.Parse($"\"{spelled}\"");
            return document.RootElement.GetString() ?? spelled;
        }
        catch (JsonException)
        {
            return spelled;
        }
    }

    /// <summary>The canonical spelling of <paramref name="text"/> as JSON string content.</summary>
    /// <param name="text">The value to spell, taken literally - it is not decoded first.</param>
    /// <remarks>
    /// Only the four short escapes are used where JSON has one; everything else below a space becomes a character
    /// reference, because a raw control character is what makes a hand-edited document unreadable.
    /// </remarks>
    internal static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var spelled = new StringBuilder(text.Length + 2);
        foreach (var character in text)
        {
            switch (character)
            {
                case '"':
                    spelled.Append("\\\"");
                    break;
                case '\\':
                    spelled.Append("\\\\");
                    break;
                case '\b':
                    spelled.Append("\\b");
                    break;
                case '\f':
                    spelled.Append("\\f");
                    break;
                case '\n':
                    spelled.Append("\\n");
                    break;
                case '\r':
                    spelled.Append("\\r");
                    break;
                case '\t':
                    spelled.Append("\\t");
                    break;
                case < ' ':
                    spelled.Append("\\u").Append(((int)character).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                default:
                    spelled.Append(character);
                    break;
            }
        }

        return spelled.ToString();
    }
}
