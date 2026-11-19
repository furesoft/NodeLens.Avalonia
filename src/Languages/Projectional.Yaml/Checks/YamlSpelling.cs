using System.Text;
using YamlDotNet.Core;

namespace Projectional.Yaml.Checks;

/// <summary>The YAML spelling rules a check needs: is this text one scalar, and how is it written between quotes.</summary>
/// <remarks>
/// A scalar holds the file's whole spelling, so a check has to ask whether that spelling could have come from a file at
/// all. The tokenizer answers it: one scalar and nothing else is a scalar, and anything that begins a key, a sequence or
/// a collection is not - which is why the rule is not restated here.
/// </remarks>
internal static class YamlSpelling
{
    /// <summary>Whether <paramref name="text"/> is one scalar the format accepts.</summary>
    /// <param name="text">The spelling a scalar cell holds.</param>
    internal static bool IsLegalScalar(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            // An empty spelling is what a cell the user cleared holds, and it prints as a null value - which YAML reads.
            return true;
        }

        try
        {
            var reader = YamlTokens.Of(text.Replace("\r\n", "\n", StringComparison.Ordinal));
            var leaves = 0;

            while (!reader.AtEnd)
            {
                switch (reader.Current.Kind)
                {
                    case YamlTokenKind.Scalar or YamlTokenKind.Alias:
                        leaves++;
                        reader.Advance();
                        break;

                    case YamlTokenKind.Other:
                        reader.Advance();
                        break;

                    default:
                        return false;
                }
            }

            return leaves == 1;
        }
        catch (YamlException)
        {
            return false;
        }
    }

    /// <summary>The canonical spelling of <paramref name="text"/> as a double-quoted scalar.</summary>
    /// <param name="text">The value to spell, taken literally - it is not decoded first.</param>
    internal static string Quote(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var spelled = new StringBuilder(text.Length + 4);
        spelled.Append('"');

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
                case '\n':
                    spelled.Append("\\n");
                    break;
                case '\r':
                    spelled.Append("\\r");
                    break;
                case '\t':
                    spelled.Append("\\t");
                    break;
                default:
                    spelled.Append(character);
                    break;
            }
        }

        spelled.Append('"');

        return spelled.ToString();
    }
}
