using Avalonia.Media;
using Avalonia.Styling;

using Projectional.Projection;

namespace Projectional.Avalonia;

/// <summary>
/// A <see cref="SyntaxPalette"/> together with the colours of the surface it is drawn on.
/// </summary>
/// <remarks>
/// <para>
/// A palette names text colours only, deliberately: the background a document is drawn on belongs to
/// the host, and a dark palette on a white surface is unreadable no matter how good its twelve text
/// colours are. This type is the smallest complete answer to that - one text palette plus the colours
/// around it - so a host that wants a dark editor passes one object rather than remembering which
/// five properties have to change together.
/// </para>
/// <para>
/// It holds colours and nothing else. Applying it is one line in the host, which keeps this type free
/// of any dependency on a live control and therefore testable without a window.
/// </para>
/// </remarks>
public sealed class SyntaxTheme
{
    private SyntaxTheme(
        SyntaxPalette palette,
        IBrush background,
        IBrush chrome,
        IBrush caret,
        IBrush highlight,
        IBrush gutter,
        IBrush lineNumber,
        MarkerPalette markers,
        bool isDark)
    {
        Palette = palette;
        Background = background;
        Chrome = chrome;
        Caret = caret;
        Highlight = highlight;
        Gutter = gutter;
        LineNumber = lineNumber;
        Markers = markers;
        IsDark = isDark;
    }

    /// <summary>Dark text on white, which is what most people expect an editor to look like.</summary>
    public static SyntaxTheme Light { get; } = new(
        SyntaxPalette.Light,
        Brushes.White,
        new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)),
        Brushes.Black,
        new SolidColorBrush(Color.FromArgb(0x40, 0x26, 0x8B, 0xD2)),
        new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xF7)),
        new SolidColorBrush(Color.FromRgb(0x23, 0x78, 0x93)),
        MarkerPalette.Light,
        isDark: false);

    /// <summary>Light text on black, for a host whose surroundings are dark.</summary>
    public static SyntaxTheme Dark { get; } = new(
        SyntaxPalette.Dark,
        new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
        new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
        Brushes.White,
        new SolidColorBrush(Color.FromArgb(0x50, 0x26, 0x4F, 0x78)),
        new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)),
        new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85)),
        MarkerPalette.Dark,
        isDark: true);

    /// <summary>The colours of the text itself.</summary>
    public SyntaxPalette Palette { get; }

    /// <summary>What is painted behind the text.</summary>
    public IBrush Background { get; }

    /// <summary>
    /// What is painted around the document - tab strip, menus, status bar.
    /// </summary>
    /// <remarks>
    /// Close to <see cref="Background"/> but not equal to it, so that the edge of the document is
    /// visible. A host that paints the chrome in the page colour loses that edge; a host that invents
    /// its own chrome colour has a second theme to keep in sync.
    /// </remarks>
    public IBrush Chrome { get; }

    /// <summary>The colour of the caret bar.</summary>
    public IBrush Caret { get; }

    /// <summary>The colour selected text is painted over.</summary>
    public IBrush Highlight { get; }

    /// <summary>
    /// What is painted behind the row numbers.
    /// </summary>
    /// <remarks>
    /// A shade apart from <see cref="Background"/> rather than a different hue, so the gutter reads as
    /// part of the page instead of as a panel next to it. A host that shows no line numbers never paints
    /// this and loses nothing.
    /// </remarks>
    public IBrush Gutter { get; }

    /// <summary>The colour the row numbers are painted in.</summary>
    /// <remarks>
    /// Dimmer than the text, because a number is orientation and not content: it must be readable when
    /// looked for and must never compete with the document.
    /// </remarks>
    public IBrush LineNumber { get; }

    /// <summary>The colours a finding is stroked in, one per severity.</summary>
    /// <remarks>
    /// Part of the theme rather than of the text palette, because a squiggle is a mark <em>around</em> the
    /// document - the same layer as the caret and the selection - and it has to stay legible against the
    /// background, not against the glyphs.
    /// </remarks>
    public MarkerPalette Markers { get; }

    /// <summary>Whether this is one of the dark themes, for a toolkit that asks light or dark.</summary>
    public bool IsDark { get; }

    /// <summary>The theme that goes with a theme variant, which is how a host follows the desktop.</summary>
    /// <param name="variant">
    /// A variant as the toolkit resolved it, not as it was requested - a host that follows the operating
    /// system asks for the <em>actual</em> variant and passes that.
    /// </param>
    /// <returns>
    /// <see cref="Dark"/> for a variant derived from dark, <see cref="Light"/> otherwise, which includes
    /// the unresolved default: a host with no opinion about its surroundings gets the theme that is
    /// readable on most of them.
    /// </returns>
    /// <remarks>
    /// The variant is walked up its inheritance chain rather than compared with
    /// <see cref="ThemeVariant.Dark"/>, because a host is allowed to define its own variant
    /// (<c>new ThemeVariant("Midnight", ThemeVariant.Dark)</c>) and one that says it derives from dark
    /// means dark. This is the only place that decides, so a host and the shell cannot disagree.
    /// </remarks>
    public static SyntaxTheme Of(ThemeVariant variant) =>
        IsDarkVariant(variant) ? Dark : Light;

    /// <summary>The text colour of one meaning, as a palette lookup.</summary>
    /// <param name="kind">What a run means.</param>
    /// <returns>The brush this theme paints that meaning in.</returns>
    public IBrush BrushOf(TextStyleKind kind) => Palette.BrushOf(kind);

    private static bool IsDarkVariant(ThemeVariant? variant) =>
        variant is not null
        && (variant == ThemeVariant.Dark || IsDarkVariant(variant.InheritVariant));
}
