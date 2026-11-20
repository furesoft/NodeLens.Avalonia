using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>
/// The glyph a folder row is drawn with, chosen by what the folder holds.
/// </summary>
/// <remarks>
/// <para>
/// A language's folders are its aspects - what it declares, how it is drawn, what gives it meaning - so one folder
/// icon for all of them would say "there is something in here" and nothing else. Each aspect gets the mark a reader
/// already associates with it: blocks for the structure, a pen for the notation, a tag for the types, a shield for
/// the constraints, a target for the scopes, a bulb for the intentions, an arrow into a tray for the import, a gear
/// for the generator and a tick for the tests.
/// </para>
/// <para>
/// The geometries are parsed on first use rather than declared as fields, because a <see cref="StreamGeometry"/>
/// needs a rendering platform: a row is built headlessly in tests, and a static initializer that touched this table
/// would make a row unbuildable without a window.
/// </para>
/// </remarks>
internal static class FolderIcons
{
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        ["structure"] = "M3,3 H13 V13 H3 Z M3,8 H13 M8,3 V13",
        ["editor"] = "M2.5,13.5 L3.5,10 L10.5,3 L13,5.5 L6,12.5 Z",
        ["types"] = "M9.5,2.5 H13.5 V6.5 L7,13 L3,9 Z",
        ["constraints"] = "M8,2 L13,4 V8 C13,11 8,14 8,14 C8,14 3,11 3,8 V4 Z",
        ["scopes"] = "M8,2.5 A5.5,5.5 0 1 0 8,13.5 A5.5,5.5 0 1 0 8,2.5 M8,5.5 A2.5,2.5 0 1 0 8,10.5 A2.5,2.5 0 1 0 8,5.5",
        ["intentions"] = "M5.5,11 C5.5,8.5 4,7.5 4,5.8 A4,4 0 1 1 12,5.8 C12,7.5 10.5,8.5 10.5,11 Z M6.5,12.5 H9.5 M7,14.2 H9",
        ["import"] = "M8,2 V9 M4.5,6 L8,9.5 L11.5,6 M3,12 V14 H13 V12",
        ["generator"] = "M8,5.5 A2.5,2.5 0 1 0 8,10.5 A2.5,2.5 0 1 0 8,5.5 M8,2 V4 M8,12 V14 M2,8 H4 M12,8 H14",
        ["tests"] = "M3,8.5 L6.5,12 L13,4.5",
    };

    private static readonly Dictionary<string, Geometry> Parsed = new(StringComparer.Ordinal);

    /// <summary>The glyph for <paramref name="folder"/>, or a plain folder when the name is not an aspect.</summary>
    /// <param name="folder">The folder's name on disk.</param>
    internal static Geometry? Of(string? folder)
    {
        if (folder is null)
        {
            return null;
        }

        if (Parsed.TryGetValue(folder, out var cached))
        {
            return cached;
        }

        if (!Glyphs.TryGetValue(folder, out var glyph))
        {
            return null;
        }

        var geometry = Geometry.Parse(glyph);
        Parsed[folder] = geometry;

        return geometry;
    }
}
