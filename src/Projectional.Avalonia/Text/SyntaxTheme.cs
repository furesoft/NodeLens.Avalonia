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
        MarkerTipPalette tip,
        IBrush bulb,
        PreviewPalette preview,
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
        Tip = tip;
        Bulb = bulb;
        Preview = preview;
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
        MarkerTipPalette.Light,
        new SolidColorBrush(Color.FromRgb(0xC8, 0x87, 0x1C)),
        PreviewPalette.Light,
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
        MarkerTipPalette.Dark,
        new SolidColorBrush(Color.FromRgb(0xE8, 0xA3, 0x3D)),
        PreviewPalette.Dark,
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

    /// <summary>The colours of the lists that are drawn onto the document.</summary>
    /// <remarks>
    /// <para>
    /// One palette for the completion list and the quick-fix list, because they are one kind of surface: a
    /// box painted over the page. It is part of the theme rather than of the text palette for the same
    /// reason <see cref="Markers"/> is - a white box on a dark page is unreadable however good its text
    /// colours are, and a host that themes the page but not its overlays has shipped a bug the user can
    /// see.
    /// </para>
    /// <para>
    /// Built on first use rather than in the constructor, and that is not laziness for its own sake: a
    /// <see cref="CompletionPalette"/> carries vector icons, and a <c>StreamGeometry</c> needs a rendering
    /// platform. Constructing it here would make the static initializer of this type require a window, and
    /// a theme is exactly the kind of value a headless test wants to compare.
    /// </para>
    /// </remarks>
    public CompletionPalette Lists => field ??= IsDark ? CompletionPalette.Dark : CompletionPalette.Light;

    /// <summary>The colours of the marker tooltip, which is the other box drawn over the page.</summary>
    public MarkerTipPalette Tip { get; }

    /// <summary>The colour of the quick-fix bulb in the margin.</summary>
    /// <remarks>
    /// A single brush rather than a palette, because the bulb says one thing - "this line offers a repair".
    /// Warmer and lighter on a dark page, because a colour that reads as an accent on white disappears into
    /// a dark background.
    /// </remarks>
    public IBrush Bulb { get; }

    /// <summary>The colours a preview's ghost is drawn in.</summary>
    /// <remarks>
    /// Part of the theme because a ghost is painted over the page: a host that themes the document but not the
    /// preview ships a light sheet on a dark background. Built eagerly, unlike <see cref="Lists"/>, because a
    /// tint is a brush and needs no rendering platform.
    /// </remarks>
    public PreviewPalette Preview { get; }

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
