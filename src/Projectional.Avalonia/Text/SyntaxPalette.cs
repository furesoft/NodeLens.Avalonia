using System.Collections.Frozen;

using Avalonia.Media;
using Avalonia.Media.Immutable;

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

    private SyntaxPalette(FrozenDictionary<TextStyleKind, IBrush> brushes) => _brushes = brushes;

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
    ]);

    /// <summary>Builds a palette from the kinds it mentions.</summary>
    /// <param name="brushes">One entry per kind; kinds left out fall back to <see cref="TextStyleKind.Default"/>.</param>
    /// <returns>The palette.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="brushes"/> or one of its values is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A kind is mentioned twice, or <see cref="TextStyleKind.Default"/> is left out and there is nothing
    /// to fall back to.
    /// </exception>
    public static SyntaxPalette Create(IEnumerable<KeyValuePair<TextStyleKind, IBrush>> brushes)
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

        return new SyntaxPalette(entries.ToFrozenDictionary());
    }

    /// <summary>The brush <paramref name="kind"/> is painted with.</summary>
    /// <param name="kind">What a run means.</param>
    /// <returns>The palette's brush for that kind, or its default brush.</returns>
    public IBrush BrushOf(TextStyleKind kind) =>
        _brushes.TryGetValue(kind, out var brush) ? brush : _brushes[TextStyleKind.Default];

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
                .ToFrozenDictionary());
    }

    private static KeyValuePair<TextStyleKind, IBrush> Entry(TextStyleKind kind, string colour) =>
        new(kind, new ImmutableSolidColorBrush(Color.Parse(colour)));
}
