using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>The mark a browser row is drawn with: the concept's initial in a colour that concept owns.</summary>
/// <remarks>
/// <para>
/// A concept is what a row <em>is</em>, so it is what the row is drawn as - the same idea the project explorer follows
/// for an aspect, applied to the model instead of to the folder. One colour per concept lets a reader see at a glance
/// that five rows are rules and the sixth is an outcome, which a column of identical page icons cannot say.
/// </para>
/// <para>
/// The colour is derived from the concept's key rather than declared: the same concept is the same colour in every
/// document of every language, which is the property that makes the colour worth having. It is a grouping cue and not
/// an identity - the palette is small, so two concepts can share a colour - and it is not a language author's choice
/// either: a language that wants its own icons is a feature this browser does not have yet. The hash is written out
/// here instead of using <see cref="string.GetHashCode()"/> because that one is randomised per process, and a colour
/// that changed between two runs of the same program would be a decoration.
/// </para>
/// </remarks>
internal static class OutlineIcons
{
    /// <summary>The colours a concept may be drawn in: mid-tones, so one table serves the light and the dark variant.</summary>
    private static readonly Color[] Palette =
    [
        Color.Parse("#3F9E52"),
        Color.Parse("#B8912A"),
        Color.Parse("#8467C4"),
        Color.Parse("#C0574B"),
        Color.Parse("#2F9DB8"),
        Color.Parse("#4A79C0"),
        Color.Parse("#D2760A"),
        Color.Parse("#8A6D3B"),
    ];

    /// <summary>The brushes handed out so far, one per concept, so a scrolled list does not allocate per row.</summary>
    private static readonly Dictionary<string, IBrush> Brushes = new(StringComparer.Ordinal);

    /// <summary>The brush for <paramref name="conceptKey"/>, or a neutral grey when there is no key.</summary>
    /// <param name="conceptKey">The concept's registry key.</param>
    internal static IBrush BrushOf(string? conceptKey)
    {
        var key = conceptKey ?? string.Empty;

        if (Brushes.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(Palette[Index(key)]);
        Brushes[key] = brush;

        return brush;
    }

    /// <summary>The letter a row of <paramref name="conceptKey"/> is drawn as.</summary>
    /// <param name="conceptKey">The concept's registry key.</param>
    /// <remarks>
    /// The concept's initial without its namespace: a row of <c>Rules.Rule</c> is a <c>R</c>, which is what a reader who
    /// knows the language already calls it.
    /// </remarks>
    internal static string BadgeOf(string? conceptKey)
    {
        if (string.IsNullOrEmpty(conceptKey))
        {
            return "?";
        }

        var name = conceptKey[(conceptKey.LastIndexOf('.') + 1)..];

        return name.Length == 0 ? "?" : name[..1].ToUpperInvariant();
    }

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
