namespace Projectional.Core;

/// <summary>
/// Spelling rules for the names that end up in a model file. Keeping them strict means a
/// slot name can never collide with the syntax of the persistence format.
/// </summary>
internal static class NameRules
{
    public static bool IsValidSlotName(string? name) => IsValid(name, allowDots: false);

    public static bool IsValidConceptKey(string? key) => IsValid(key, allowDots: true);

    private static bool IsValid(string? text, bool allowDots)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var atSegmentStart = true;
        foreach (var character in text)
        {
            if (character == '.')
            {
                if (!allowDots || atSegmentStart)
                {
                    return false;
                }

                atSegmentStart = true;
                continue;
            }

            if (atSegmentStart)
            {
                if (!char.IsAsciiLetter(character) && character != '_')
                {
                    return false;
                }

                atSegmentStart = false;
            }
            else if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return !atSegmentStart;
    }
}
