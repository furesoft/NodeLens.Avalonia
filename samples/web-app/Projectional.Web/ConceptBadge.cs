namespace Projectional.Web;

/// <summary>The mark a model-browser row is drawn with: the concept's initial in a colour that concept owns.</summary>
/// <remarks>
/// <para>
/// A concept is what a row <em>is</em>, so it is what the row is drawn as. One colour per concept lets a reader see at
/// a glance that five rows are rules and the sixth is an outcome, which a column of identical page icons cannot say.
/// </para>
/// <para>
/// The colour is derived from the concept's key rather than declared, which is the property that makes it worth
/// having: the same concept is the same colour in every document of every language. It is a grouping cue and not an
/// identity - the palette is small, so two concepts can share a colour. The hash is written out instead of using
/// <see cref="string.GetHashCode()"/> because that one is randomised per process, and a colour that changed between two
/// runs of the same program would be a decoration.
/// </para>
/// <para>
/// The palette and the hash are the desktop workbench's (<c>OutlineIcons</c>) so a reader who moves between the two
/// sees the same colour for the same concept.
/// </para>
/// </remarks>
public static class ConceptBadge
{
    /// <summary>The colours a concept may be drawn in: mid-tones, so one palette serves light and dark.</summary>
    private static readonly string[] Palette =
    [
        "#3F9E52",
        "#B8912A",
        "#8467C4",
        "#C0574B",
        "#2F9DB8",
        "#4A79C0",
        "#D2760A",
        "#8A6D3B",
    ];

    /// <summary>The letter a row of <paramref name="conceptKey"/> is drawn as.</summary>
    /// <param name="conceptKey">The concept's registry key.</param>
    /// <remarks>
    /// The concept's initial without its namespace: a row of <c>Rules.Rule</c> is a <c>R</c>, which is what a reader
    /// who knows the language already calls it.
    /// </remarks>
    public static string LetterOf(string? conceptKey)
    {
        if (string.IsNullOrEmpty(conceptKey))
        {
            return "?";
        }

        var name = conceptKey[(conceptKey.LastIndexOf('.') + 1)..];

        return name.Length == 0 ? "?" : name[..1].ToUpperInvariant();
    }

    /// <summary>The CSS colour <paramref name="conceptKey"/> owns.</summary>
    /// <param name="conceptKey">The concept's registry key.</param>
    public static string ColorOf(string? conceptKey) => Palette[Index(conceptKey ?? string.Empty)];

    /// <summary>Which palette entry <paramref name="key"/> owns.</summary>
    /// <remarks>
    /// FNV-1a over the key's characters: a fixed, documented hash, so the answer is the same in every process and on
    /// every machine. It is not a security boundary and does not need to be one - it only has to be stable.
    /// </remarks>
    private static int Index(string key)
    {
        const uint Offset = 2166136261;
        const uint Prime = 16777619;

        var hash = Offset;

        foreach (var character in key)
        {
            hash = (hash ^ character) * Prime;
        }

        return (int)(hash % Palette.Length);
    }
}
