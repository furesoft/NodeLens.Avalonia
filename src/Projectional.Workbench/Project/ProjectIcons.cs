using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>
/// The mark a project explorer row is drawn with: a glyph for a folder or a project, a letter badge for a file, and
/// one colour per aspect so the tree reads like the model it stands for.
/// </summary>
/// <remarks>
/// <para>
/// A language's folders are its aspects - what it declares, how it is drawn, and what gives it meaning - so one
/// folder icon and one grey for all of them would say "there is something in here" and nothing else. Each aspect
/// gets the glyph and the colour a reader already associates with it, which is the convention the tools this editor
/// follows use: structure is green, the editor is amber, the typesystem is violet, the generator is orange.
/// </para>
/// <para>
/// Colour is the aspect, the glyph is the aspect's own mark, and a file's badge letter is the kind of definition it
/// holds - a concept under <c>structure</c>, an editor under <c>editor</c>. That is what a reader of the tree wants
/// to tell apart at a glance, and it is what a folder name alone cannot say.
/// </para>
/// <para>
/// The geometries are parsed on first use rather than declared as fields, because a <see cref="StreamGeometry"/>
/// needs a rendering platform: a row is built headlessly in tests, and a static initializer that touched this table
/// would make a row unbuildable without a window. The letter table holds no such type, so a row can ask it without
/// a platform and a test can assert what a row will be drawn as.
/// </para>
/// </remarks>
internal static class ProjectIcons
{
    /// <summary>The key of a language project's own row.</summary>
    internal const string ProjectKey = "project";

    /// <summary>The key of an open document's row.</summary>
    internal const string DocumentKey = "document";

    /// <summary>The key of a folder that is not one of a language's aspects.</summary>
    internal const string FolderKey = "folder";

    /// <summary>The key of a definition file whose aspect is unknown.</summary>
    internal const string FileKey = "file";

    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        [ProjectKey] = "M2,4 L8,1 L14,4 L8,7 Z M2,4 V11 L8,14 V7 M14,4 V11 L8,14",
        [DocumentKey] = "M3,1.5 H9.5 L12.5,4.5 V14.5 H3 Z M9.5,1.5 V4.5 H12.5",
        [FileKey] = "M3,1.5 H9.5 L12.5,4.5 V14.5 H3 Z M9.5,1.5 V4.5 H12.5",
        [FolderKey] = "M1,3.5 H6 L7.5,5.5 H15 V13 H1 Z",
        ["structure"] = "M3,3 H13 V13 H3 Z M3,8 H13 M8,3 V13",
        ["editor"] = "M2.5,13.5 L3.5,10 L10.5,3 L13,5.5 L6,12.5 Z",
        ["types"] = "M9.5,2.5 H13.5 V6.5 L7,13 L3,9 Z",
        ["constraints"] = "M8,2 L13,4 V8 C13,11 8,14 8,14 C8,14 3,11 3,8 V4 Z",
        ["scopes"] = "M8,2.5 A5.5,5.5 0 1 0 8,13.5 A5.5,5.5 0 1 0 8,2.5 M8,5.5 A2.5,2.5 0 1 0 8,10.5 A2.5,2.5 0 1 0 8,5.5",
        ["typing"] = "M2.5,4 H13.5 V12 H2.5 Z M4.5,6 H5.5 M7,6 H8 M9.5,6 H10.5 M4.5,9 H10.5",
        ["intentions"] = "M5.5,11 C5.5,8.5 4,7.5 4,5.8 A4,4 0 1 1 12,5.8 C12,7.5 10.5,8.5 10.5,11 Z M6.5,12.5 H9.5 M7,14.2 H9",
        ["import"] = "M8,2 V9 M4.5,6 L8,9.5 L11.5,6 M3,12 V14 H13 V12",
        ["generator"] = "M8,5.5 A2.5,2.5 0 1 0 8,10.5 A2.5,2.5 0 1 0 8,5.5 M8,2 V4 M8,12 V14 M2,8 H4 M12,8 H14",
        ["tests"] = "M3,8.5 L6.5,12 L13,4.5",
    };

    /// <summary>The colour of each aspect: mid-tones, so one table serves the light and the dark variant.</summary>
    private static readonly Dictionary<string, Color> Colors = new(StringComparer.Ordinal)
    {
        [ProjectKey] = Color.Parse("#A6791C"),
        [DocumentKey] = Color.Parse("#5F6B7A"),
        [FileKey] = Color.Parse("#5F6B7A"),
        [FolderKey] = Color.Parse("#7A7A7A"),
        ["structure"] = Color.Parse("#3F9E52"),
        ["editor"] = Color.Parse("#B8912A"),
        ["types"] = Color.Parse("#8467C4"),
        ["constraints"] = Color.Parse("#C0574B"),
        ["scopes"] = Color.Parse("#2F9DB8"),
        ["typing"] = Color.Parse("#C1568F"),
        ["intentions"] = Color.Parse("#A98C00"),
        ["import"] = Color.Parse("#4A79C0"),
        ["generator"] = Color.Parse("#D2760A"),
        ["tests"] = Color.Parse("#2E9E5B"),
    };

    /// <summary>
    /// The letter a file's badge shows: the kind of definition that aspect holds, which is what a reader looks for
    /// under a folder rather than which folder it is in.
    /// </summary>
    /// <remarks>
    /// <c>P</c> is a typing rule, which is a predicate on the typed text; <c>R</c> is a constraint rule, <c>S</c> a
    /// scope rule and <c>M</c> an importer, because <c>I</c>, <c>C</c> and <c>E</c> were taken by the kinds a reader
    /// meets first.
    /// </remarks>
    private static readonly Dictionary<string, string> Badges = new(StringComparer.Ordinal)
    {
        ["structure"] = "C",
        ["editor"] = "E",
        ["types"] = "T",
        ["constraints"] = "R",
        ["scopes"] = "S",
        ["typing"] = "P",
        ["intentions"] = "I",
        ["import"] = "M",
        ["generator"] = "G",
        ["tests"] = "\u2713",
    };

    private static readonly Dictionary<string, Geometry> Parsed = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, IBrush> Brushes = new(StringComparer.Ordinal);

    /// <summary>The glyph for <paramref name="key"/>, or <see langword="null"/> when the key has none.</summary>
    /// <param name="key">An aspect folder's name, or one of the keys this table declares.</param>
    internal static Geometry? GlyphOf(string? key)
    {
        if (key is null)
        {
            return null;
        }

        if (Parsed.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!Glyphs.TryGetValue(key, out var glyph))
        {
            return null;
        }

        var geometry = Geometry.Parse(glyph);
        Parsed[key] = geometry;

        return geometry;
    }

    /// <summary>The colour for <paramref name="key"/>, which is a neutral grey for a key this table does not know.</summary>
    /// <param name="key">An aspect folder's name, or one of the keys this table declares.</param>
    internal static IBrush BrushOf(string? key)
    {
        var brushKey = key ?? FolderKey;

        if (Brushes.TryGetValue(brushKey, out var cached))
        {
            return cached;
        }

        var brush = new SolidColorBrush(Colors.TryGetValue(brushKey, out var colour) ? colour : Colors[FolderKey]);
        Brushes[brushKey] = brush;

        return brush;
    }

    /// <summary>The badge letter for a file of <paramref name="aspect"/>, or <see langword="null"/> when it has none.</summary>
    /// <param name="aspect">The aspect folder the file sits in.</param>
    /// <remarks>
    /// A table of letters rather than a rendering decision, so a row can answer it headlessly: that is what lets a
    /// test say what a file row will be drawn as without a window.
    /// </remarks>
    internal static string? BadgeOf(string? aspect) =>
        aspect is not null && Badges.TryGetValue(aspect, out var badge) ? badge : null;
}
