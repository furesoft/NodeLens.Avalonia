using Avalonia.Media;

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
        bool isDark)
    {
        Palette = palette;
        Background = background;
        Chrome = chrome;
        Caret = caret;
        Highlight = highlight;
        IsDark = isDark;
    }

    /// <summary>Dark text on white, which is what most people expect an editor to look like.</summary>
    public static SyntaxTheme Light { get; } = new(
        SyntaxPalette.Light,
        Brushes.White,
        new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3)),
        Brushes.Black,
        new SolidColorBrush(Color.FromArgb(0x40, 0x26, 0x8B, 0xD2)),
        isDark: false);

    /// <summary>Light text on black, for a host whose surroundings are dark.</summary>
    public static SyntaxTheme Dark { get; } = new(
        SyntaxPalette.Dark,
        new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
        new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
        Brushes.White,
        new SolidColorBrush(Color.FromArgb(0x50, 0x26, 0x4F, 0x78)),
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

    /// <summary>Whether this is one of the dark themes, for a toolkit that asks light or dark.</summary>
    public bool IsDark { get; }

    /// <summary>The text colour of one meaning, as a palette lookup.</summary>
    /// <param name="kind">What a run means.</param>
    /// <returns>The brush this theme paints that meaning in.</returns>
    public IBrush BrushOf(TextStyleKind kind) => Palette.BrushOf(kind);
}
