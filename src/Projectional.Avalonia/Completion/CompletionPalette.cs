using System.Collections.Frozen;

using Avalonia.Media;
using Avalonia.Media.Immutable;

using Projectional.Editing;

namespace Projectional.Avalonia;

/// <summary>
/// Turns the kind of a completion proposal into the icon it is marked with, plus the colours of the popup.
/// </summary>
/// <remarks>
/// <para>
/// The same split as <see cref="SyntaxPalette"/>, one layer up: the editing layer says which proposals a
/// slot has and what each one stands for, and a palette says what that looks like. Nothing here reaches
/// the model, so a palette can be swapped without re-projecting anything - the proposals are recomputed
/// from the language every time the caret lands in an empty slot.
/// </para>
/// <para>
/// An icon is an <see cref="CompletionIcon"/>, which carries an <see cref="IImage"/>. The presets declare
/// theirs as vector paths so that no binary asset has to be committed and the pictures stay crisp at any
/// scale, but a host is free to pass a <c>Bitmap</c> instead - the presenter only ever draws an
/// <see cref="IImage"/> into the box it was given.
/// </para>
/// <para>
/// A kind the palette does not mention falls back to a plainly coloured marker rather than to nothing, so
/// a host that only cares about one kind still gets a readable list.
/// </para>
/// </remarks>
public sealed class CompletionPalette
{
    /// <summary>The side of the box an icon is drawn into, in layout units.</summary>
    public const double IconSize = 16;

    /// <summary>The shape of a proposal that inserts a node.</summary>
    private const string NodePath = "M 8,1 L 15,8 L 8,15 L 1,8 Z";

    /// <summary>The shape drawn for a kind the palette does not mention.</summary>
    private const string MarkerPath = "M 5,5 L 11,5 L 11,11 L 5,11 Z";

    private readonly FrozenDictionary<CompletionKind, CompletionIcon> _icons;

    private CompletionPalette(
        IBrush background,
        IBrush border,
        IBrush foreground,
        IBrush selectionBackground,
        IBrush selectionForeground,
        FrozenDictionary<CompletionKind, CompletionIcon> icons)
    {
        Background = background;
        Border = border;
        Foreground = foreground;
        SelectionBackground = selectionBackground;
        SelectionForeground = selectionForeground;
        _icons = icons;
    }

    /// <summary>The palette for a light background.</summary>
    public static CompletionPalette Light { get; } = Create(
        Paint("#FFFFFF"),
        Paint("#C8C8C8"),
        Paint("#1E1E1E"),
        Paint("#0060C0"),
        Paint("#FFFFFF"),
        [Icon(CompletionKind.Node, NodePath, "#B06A00")]);

    /// <summary>The palette for a dark background.</summary>
    public static CompletionPalette Dark { get; } = Create(
        Paint("#252526"),
        Paint("#3F3F46"),
        Paint("#D4D4D4"),
        Paint("#04395E"),
        Paint("#FFFFFF"),
        [Icon(CompletionKind.Node, NodePath, "#E0A030")]);

    /// <summary>What is painted behind the rows.</summary>
    public IBrush Background { get; }

    /// <summary>The line around the popup, which is what separates it from the document behind it.</summary>
    public IBrush Border { get; }

    /// <summary>The colour of a label, and of the marker of a kind the palette does not mention.</summary>
    public IBrush Foreground { get; }

    /// <summary>What the row under the highlight is painted with.</summary>
    public IBrush SelectionBackground { get; }

    /// <summary>The colour of the label on the highlighted row.</summary>
    public IBrush SelectionForeground { get; }

    /// <summary>Builds a palette.</summary>
    /// <param name="background">What is painted behind the rows.</param>
    /// <param name="border">The line around the popup.</param>
    /// <param name="foreground">The colour of labels and of unmentioned kinds.</param>
    /// <param name="selectionBackground">What the highlighted row is painted with.</param>
    /// <param name="selectionForeground">The colour of the label on the highlighted row.</param>
    /// <param name="icons">One icon per kind; kinds left out get a plain marker.</param>
    /// <returns>The palette.</returns>
    /// <exception cref="ArgumentNullException">A brush, or an icon, is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A kind is mentioned twice.</exception>
    public static CompletionPalette Create(
        IBrush background,
        IBrush border,
        IBrush foreground,
        IBrush selectionBackground,
        IBrush selectionForeground,
        IEnumerable<KeyValuePair<CompletionKind, CompletionIcon>> icons)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(border);
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(selectionBackground);
        ArgumentNullException.ThrowIfNull(selectionForeground);
        ArgumentNullException.ThrowIfNull(icons);

        var entries = icons.ToArray();

        foreach (var entry in entries)
        {
            if (entry.Value is null)
            {
                throw new ArgumentNullException(nameof(icons), $"The palette has no icon for {entry.Key}.");
            }
        }

        // Checked by hand rather than left to the dictionary, for the same reason as in the syntax
        // palette: which of two icons wins must never be decided by the order a host happened to build
        // its entries in.
        var seen = new HashSet<CompletionKind>();

        foreach (var entry in entries)
        {
            if (!seen.Add(entry.Key))
            {
                throw new ArgumentException($"The palette mentions {entry.Key} twice.", nameof(icons));
            }
        }

        return new CompletionPalette(
            background,
            border,
            foreground,
            selectionBackground,
            selectionForeground,
            entries.ToFrozenDictionary());
    }

    /// <summary>How a proposal of <paramref name="kind"/> is marked.</summary>
    /// <param name="kind">What a proposal stands for.</param>
    /// <returns>The palette's icon for that kind, or a plain marker when it has none.</returns>
    public CompletionIcon IconOf(CompletionKind kind) =>
        _icons.TryGetValue(kind, out var icon) ? icon : Marker;

    /// <summary>This palette with one kind's icon changed.</summary>
    /// <param name="kind">The kind to remark.</param>
    /// <param name="icon">The icon to draw for it.</param>
    /// <returns>A new palette; this one is unchanged, so a palette can be shared and amended safely.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="icon"/> is <see langword="null"/>.</exception>
    public CompletionPalette With(CompletionKind kind, CompletionIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);

        return new CompletionPalette(
            Background,
            Border,
            Foreground,
            SelectionBackground,
            SelectionForeground,
            _icons
                .Select(entry => entry.Key == kind
                    ? new KeyValuePair<CompletionKind, CompletionIcon>(kind, icon)
                    : entry)
                .ToFrozenDictionary());
    }

    /// <inheritdoc />
    public override string ToString() => $"{_icons.Count} icon(s)";

    /// <summary>The marker of a kind the palette does not mention, in the label colour.</summary>
    private CompletionIcon Marker => field ??= CompletionIcon.FromPath(MarkerPath, Foreground, IconSize);

    private static ImmutableSolidColorBrush Paint(string colour) => new(Color.Parse(colour));

    private static KeyValuePair<CompletionKind, CompletionIcon> Icon(
        CompletionKind kind,
        string path,
        string colour) =>
        new(kind, CompletionIcon.FromPath(path, Paint(colour), IconSize));
}
