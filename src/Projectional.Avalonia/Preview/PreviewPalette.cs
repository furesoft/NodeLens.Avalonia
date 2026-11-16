using Avalonia.Media;

using Projectional.Editing;

namespace Projectional.Avalonia;

/// <summary>
/// The colours a preview's ghost is drawn in: what an edit added, changed or removed, and how the rest is
/// quietened.
/// </summary>
/// <remarks>
/// <para>
/// Part of the theme for the same reason the marker and list palettes are: a ghost is painted <em>over</em> the
/// document's surface, so a host that themes the page but not the ghost ships a white sheet on a dark page. The
/// tints are translucent on purpose - the ghost is a document, not a diagram, and the text under a tint has to
/// stay readable.
/// </para>
/// <para>
/// The palette holds brushes and nothing else, so it needs no rendering platform and a headless test can compare
/// two themes. It names <em>meanings</em>, not colours, exactly like <see cref="SyntaxPalette"/>: which tint an
/// addition gets is this type's business, and nothing above it knows a colour.
/// </para>
/// </remarks>
public sealed class PreviewPalette
{
    private PreviewPalette(IBrush added, IBrush changed, IBrush removed, IBrush dimmed)
    {
        Added = added;
        Changed = changed;
        Removed = removed;
        Dimmed = dimmed;
    }

    /// <summary>Tints for a light page: green for new, amber for changed, red for gone.</summary>
    public static PreviewPalette Light { get; } = new(
        Paint(0x40, 0x2E, 0x9E, 0x4F),
        Paint(0x40, 0xC8, 0x87, 0x1C),
        Paint(0x40, 0xC0, 0x39, 0x2B),
        Paint(0xFF, 0x9A, 0x9A, 0x9A));

    /// <summary>Tints for a dark page, lighter so they read against a dark background.</summary>
    public static PreviewPalette Dark { get; } = new(
        Paint(0x50, 0x5C, 0xC8, 0x7A),
        Paint(0x50, 0xE0, 0xA4, 0x3C),
        Paint(0x50, 0xE0, 0x6C, 0x5C),
        Paint(0xFF, 0x78, 0x78, 0x78));

    /// <summary>The tint behind a cell the edit created.</summary>
    public IBrush Added { get; }

    /// <summary>The tint behind a cell whose value or place differs.</summary>
    public IBrush Changed { get; }

    /// <summary>The tint behind the place a node was removed from.</summary>
    public IBrush Removed { get; }

    /// <summary>The colour everything the edit did <em>not</em> touch is painted in.</summary>
    /// <remarks>
    /// A grey rather than a dimmed version of each text colour: the marks are the subject, and one muted colour
    /// for the rest says "look there" without inventing a second syntax palette.
    /// </remarks>
    public IBrush Dimmed { get; }

    /// <summary>The tint for one kind of change.</summary>
    /// <param name="kind">What happened to the cell.</param>
    public IBrush BrushOf(CellChangeKind kind) => kind switch
    {
        CellChangeKind.Added => Added,
        CellChangeKind.Changed => Changed,
        CellChangeKind.Removed => Removed,
        _ => Changed,
    };

    public override string ToString() => $"preview, dimmed in {Dimmed}";

    private static SolidColorBrush Paint(byte alpha, byte red, byte green, byte blue) =>
        new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
}
