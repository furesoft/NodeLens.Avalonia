using Avalonia.Media;

namespace Projectional.Workbench;

/// <summary>The four surfaces a terminal paints, and the rule that turns a cell's colour into a brush.</summary>
/// <remarks>
/// <para>
/// The emulator answers with the colour a cell actually has, resolved from whatever the program wrote - a named ANSI
/// colour, a 256-colour index, a 24-bit value - so this palette cannot be a table of sixteen entries. It maps the two
/// colours that mean "the default" onto the theme's own and lets every other colour through, which is what keeps a
/// coloured program legible on a light page instead of painting the emulator's black default over it.
/// </para>
/// <para>
/// The values it compares against are VtNetCore's own defaults: a cell with no colour set reports <c>#CDCDCD</c> on
/// <c>#000000</c>.
/// </para>
/// </remarks>
/// <param name="Background">The page the cells are painted on.</param>
/// <param name="Foreground">What a cell with no colour set is written in.</param>
/// <param name="Cursor">The block that marks the cursor while the terminal has the focus.</param>
internal sealed record TerminalPalette(IBrush Background, IBrush Foreground, IBrush Cursor)
{
    /// <summary>VtNetCore's answer for "no background colour was set".</summary>
    private const string DefaultBackground = "#000000";

    /// <summary>VtNetCore's answer for "no foreground colour was set".</summary>
    private const string DefaultForeground = "#CDCDCD";

    private static readonly TerminalPalette LightPalette = new(
        new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)),
        new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)),
        new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)));

    private static readonly TerminalPalette DarkPalette = new(
        new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
        new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xDC)),
        new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));

    /// <summary>The palette the light theme paints with.</summary>
    internal static TerminalPalette Light => LightPalette;

    /// <summary>The palette the dark theme paints with.</summary>
    internal static TerminalPalette Dark => DarkPalette;

    /// <summary>The brush a cell's background is painted with.</summary>
    /// <param name="webColor">The colour the emulator reported, or <see langword="null"/> for none.</param>
    internal IBrush BackgroundOf(string? webColor) =>
        string.IsNullOrEmpty(webColor) || webColor == DefaultBackground
            ? Background
            : Parse(webColor);

    /// <summary>The brush a cell's text is written with.</summary>
    /// <param name="webColor">The colour the emulator reported, or <see langword="null"/> for none.</param>
    internal IBrush ForegroundOf(string? webColor) =>
        string.IsNullOrEmpty(webColor) || webColor == DefaultForeground
            ? Foreground
            : Parse(webColor);

    /// <summary>The brush for one hex colour, falling back to the default text colour.</summary>
    /// <remarks>
    /// A colour the theme cannot parse came from the program, so refusing it would lose the text. It is written in the
    /// default colour instead, which keeps the word on screen.
    /// </remarks>
    private IBrush Parse(string webColor) =>
        Color.TryParse(webColor, out var colour) ? new SolidColorBrush(colour) : Foreground;
}
