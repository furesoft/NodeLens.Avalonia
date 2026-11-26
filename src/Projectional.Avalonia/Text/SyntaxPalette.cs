using System.Collections.Frozen;

using Avalonia.Media;
using Avalonia.Media.Immutable;

using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Avalonia;

/// <summary>
/// Turns the meaning of a run into the brush it is painted with, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// This is the other half of highlighting. <see cref="TextStyleKind"/> says what a piece of text is -
/// a keyword, a stored string, a reference that does not resolve - and a palette says what that looks
/// like. Keeping them apart is what lets the same projection be painted by a light palette, a dark palette
/// or a host's brand colours without the language knowing anything about colour.
/// </para>
/// <para>
/// A palette varies the brush only, never the font. Layout measures a cell's text before the frame is
/// built, and the frame's bounds are what the caret, the hit test and the selection rectangles are
/// computed from; a run made bold would be wider than the box the layout gave it, and the caret would sit
/// a few pixels off. Colour is the one property of text that changes nothing else.
/// </para>
/// <para>
/// A kind the palette does not mention falls back to <see cref="TextStyleKind.Default"/>, so a partial
/// palette is still a usable palette - a host that only wants its keywords in its own colour can say
/// just that.
/// </para>
/// </remarks>
public sealed class SyntaxPalette
{
    private readonly FrozenDictionary<TextStyleKind, IBrush> _brushes;
    private readonly FrozenDictionary<PaintRole, IBrush> _fills;
    private readonly FrozenDictionary<PaintRole, IBrush> _borders;

    // Pens are built on demand and kept, because a bordered table asks for the same pen once per cell and a pen is
    // an allocation the frame would otherwise repeat for every row.
    private readonly Dictionary<(PaintRole Role, double Thickness), IPen> _pens = [];

    /// <summary>
    /// The brushes a palette uses when a role is not mentioned.
    /// </summary>
    /// <remarks>
    /// Declared before the two shipped palettes on purpose: static field initialisers run in textual order, so a
    /// fallback declared after them would still be null when they are built - which is a crash in a static
    /// constructor, not a missing colour.
    /// </remarks>
    private static readonly PaintBrushes[] NeutralPaints =
    [
        new(PaintRole.Neutral, new ImmutableSolidColorBrush(Color.Parse("#EDEDED")), new ImmutableSolidColorBrush(Color.Parse("#B0B0B0"))),
        new(PaintRole.Muted, new ImmutableSolidColorBrush(Color.Parse("#E4E4E4")), new ImmutableSolidColorBrush(Color.Parse("#A0A0A0"))),
        new(PaintRole.Accent, new ImmutableSolidColorBrush(Color.Parse("#DCE9F7")), new ImmutableSolidColorBrush(Color.Parse("#7FA8D0"))),
        new(PaintRole.Warning, new ImmutableSolidColorBrush(Color.Parse("#FBF0C8")), new ImmutableSolidColorBrush(Color.Parse("#C9A82C"))),
        new(PaintRole.Danger, new ImmutableSolidColorBrush(Color.Parse("#F7DCDE")), new ImmutableSolidColorBrush(Color.Parse("#C24444"))),
    ];

    private SyntaxPalette(
        FrozenDictionary<TextStyleKind, IBrush> brushes,
        FrozenDictionary<PaintRole, IBrush> fills,
        FrozenDictionary<PaintRole, IBrush> borders)
    {
        _brushes = brushes;
        _fills = fills;
        _borders = borders;
    }

    /// <summary>The palette for a light background.</summary>
    public static SyntaxPalette Light { get; } = Create(
    [
        Entry(TextStyleKind.Default, "#1E1E1E"),
        Entry(TextStyleKind.Keyword, "#0000C0"),
        Entry(TextStyleKind.Punctuation, "#5A5A5A"),
        Entry(TextStyleKind.Identifier, "#1E1E1E"),
        Entry(TextStyleKind.Number, "#098658"),
        Entry(TextStyleKind.Text, "#A31515"),
        Entry(TextStyleKind.Reference, "#1E7FBF"),
        Entry(TextStyleKind.Unresolved, "#C00000"),
        Entry(TextStyleKind.Placeholder, "#8A8A8A"),
        Entry(TextStyleKind.Missing, "#C00000"),
        Entry(TextStyleKind.Annotation, "#9A9A9A"),
    ],
    [
        Paint(PaintRole.Neutral, "#F4F4F4", "#C8C8C8"),
        Paint(PaintRole.Muted, "#EBEBEB", "#B4B4B4"),
        Paint(PaintRole.Accent, "#E4F0FB", "#8FB8DE"),
        Paint(PaintRole.Warning, "#FFF4CE", "#D8B32B"),
        Paint(PaintRole.Danger, "#FDE7E9", "#D14B4B"),
    ]);

    /// <summary>The palette for a dark background.</summary>
    public static SyntaxPalette Dark { get; } = Create(
    [
        Entry(TextStyleKind.Default, "#D4D4D4"),
        Entry(TextStyleKind.Keyword, "#569CD6"),
        Entry(TextStyleKind.Punctuation, "#9A9A9A"),
        Entry(TextStyleKind.Identifier, "#D4D4D4"),
        Entry(TextStyleKind.Number, "#B5CEA8"),
        Entry(TextStyleKind.Text, "#CE9178"),
        Entry(TextStyleKind.Reference, "#4EC9B0"),
        Entry(TextStyleKind.Unresolved, "#F44747"),
        Entry(TextStyleKind.Placeholder, "#6A6A6A"),
        Entry(TextStyleKind.Missing, "#F44747"),
        Entry(TextStyleKind.Annotation, "#7E7E7E"),
    ],
    [
        Paint(PaintRole.Neutral, "#2A2A2A", "#4A4A4A"),
        Paint(PaintRole.Muted, "#333333", "#454545"),
        Paint(PaintRole.Accent, "#1F3550", "#3E6A96"),
        Paint(PaintRole.Warning, "#3A3320", "#7A6A2E"),
        Paint(PaintRole.Danger, "#3D1F22", "#8A3A3A"),
    ]);

    /// <summary>Builds a palette from the kinds it mentions.</summary>
    /// <param name="brushes">One entry per kind; kinds left out fall back to <see cref="TextStyleKind.Default"/>.</param>
    /// <param name="paints">
    /// One entry per decoration role; roles left out fall back to a neutral grey that reads on either background.
    /// </param>
    /// <returns>The palette.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="brushes"/> or one of its values is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A kind is mentioned twice, or <see cref="TextStyleKind.Default"/> is left out and there is nothing
    /// to fall back to.
    /// </exception>
    public static SyntaxPalette Create(
        IEnumerable<KeyValuePair<TextStyleKind, IBrush>> brushes,
        IEnumerable<PaintBrushes>? paints = null)
    {
        ArgumentNullException.ThrowIfNull(brushes);

        var entries = brushes.ToArray();

        foreach (var entry in entries)
        {
            if (entry.Value is null)
            {
                throw new ArgumentNullException(nameof(brushes), $"The palette has no brush for {entry.Key}.");
            }
        }

        // Falling back is only safe if there is something to fall back to, and a palette without a
        // default would throw at paint time on the one kind its author never thought about.
        if (!entries.Any(entry => entry.Key == TextStyleKind.Default))
        {
            throw new ArgumentException(
                $"A palette has to say how {TextStyleKind.Default} looks.",
                nameof(brushes));
        }

        // Checked by hand rather than left to the dictionary: which of two brushes wins must never be
        // decided by the order a host happened to build its entries in.
        var seen = new HashSet<TextStyleKind>();

        foreach (var entry in entries)
        {
            if (!seen.Add(entry.Key))
            {
                throw new ArgumentException(
                    $"The palette mentions {entry.Key} twice.",
                    nameof(brushes));
            }
        }

        var declared = paints?.ToArray() ?? [];

        foreach (var paint in declared)
        {
            if (paint.Fill is null || paint.Border is null)
            {
                throw new ArgumentNullException(nameof(paints), $"The palette has no brushes for {paint.Role}.");
            }
        }

        var seenPaints = new HashSet<PaintRole>();

        foreach (var paint in declared)
        {
            if (!seenPaints.Add(paint.Role))
            {
                throw new ArgumentException(
                    $"The palette mentions {paint.Role} twice.",
                    nameof(paints));
            }
        }

        var fills = declared
            .Select(paint => new KeyValuePair<PaintRole, IBrush>(paint.Role, paint.Fill))
            .Concat(NeutralPaints.Select(paint => new KeyValuePair<PaintRole, IBrush>(paint.Role, paint.Fill)))
            .DistinctBy(entry => entry.Key)
            .ToFrozenDictionary();

        var borders = declared
            .Select(paint => new KeyValuePair<PaintRole, IBrush>(paint.Role, paint.Border))
            .Concat(NeutralPaints.Select(paint => new KeyValuePair<PaintRole, IBrush>(paint.Role, paint.Border)))
            .DistinctBy(entry => entry.Key)
            .ToFrozenDictionary();

        return new SyntaxPalette(entries.ToFrozenDictionary(), fills, borders);
    }

    /// <summary>The brush <paramref name="kind"/> is painted with.</summary>
    /// <param name="kind">What a run means.</param>
    /// <returns>The palette's brush for that kind, or its default brush.</returns>
    public IBrush BrushOf(TextStyleKind kind) =>
        _brushes.TryGetValue(kind, out var brush) ? brush : _brushes[TextStyleKind.Default];

    /// <summary>The brush a region of <paramref name="role"/> is filled with.</summary>
    /// <param name="role">What the region means.</param>
    /// <returns>The palette's fill brush for that role, or its neutral one.</returns>
    public IBrush BrushOf(PaintRole role) =>
        _fills.TryGetValue(role, out var brush) ? brush : _fills[PaintRole.Neutral];

    /// <summary>The pen a region of <paramref name="role"/> is outlined with.</summary>
    /// <param name="role">What the region means.</param>
    /// <param name="thickness">The line's width in layout units.</param>
    /// <returns>A pen, kept so the same outline is not built once per cell in a frame.</returns>
    public IPen PenOf(PaintRole role, double thickness)
    {
        var key = (role, thickness);

        if (!_pens.TryGetValue(key, out var pen))
        {
            pen = new Pen(_borders.TryGetValue(role, out var brush) ? brush : _borders[PaintRole.Neutral], thickness);
            _pens[key] = pen;
        }

        return pen;
    }

    /// <summary>This palette with one kind changed.</summary>
    /// <param name="kind">The kind to repaint.</param>
    /// <param name="brush">The brush to paint it with.</param>
    /// <returns>A new palette; this one is unchanged, so a palette can be shared and amended safely.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="brush"/> is <see langword="null"/>.</exception>
    public SyntaxPalette With(TextStyleKind kind, IBrush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);

        return new SyntaxPalette(
            _brushes
                .Select(entry => entry.Key == kind ? new KeyValuePair<TextStyleKind, IBrush>(kind, brush) : entry)
                .ToFrozenDictionary(),
            _fills,
            _borders);
    }

    private static KeyValuePair<TextStyleKind, IBrush> Entry(TextStyleKind kind, string colour) =>
        new(kind, new ImmutableSolidColorBrush(Color.Parse(colour)));

    private static PaintBrushes Paint(PaintRole role, string fill, string border) =>
        new(role, new ImmutableSolidColorBrush(Color.Parse(fill)), new ImmutableSolidColorBrush(Color.Parse(border)));
}
