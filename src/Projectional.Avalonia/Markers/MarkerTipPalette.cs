using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Projectional.Avalonia;

/// <summary>The colours of the box that describes a marker.</summary>
/// <remarks>
/// <para>
/// A palette of its own rather than a fifth brush on <see cref="MarkerPalette"/>, which exists to say what
/// one severity looks like and nothing else. The tip is chrome - it has a background, an outline and one
/// text colour - and the severity is already in the text it prints, so a severity-coloured border would be
/// the same information twice.
/// </para>
/// <para>
/// The background is opaque on purpose. The tip is drawn onto the editor's own surface, so a translucent
/// one would show the document through the words that are meant to be read.
/// </para>
/// </remarks>
public sealed class MarkerTipPalette
{
    private MarkerTipPalette(IBrush background, IBrush border, IBrush foreground)
    {
        Background = background;
        Border = border;
        Foreground = foreground;
    }

    /// <summary>The palette for a light background.</summary>
    /// <remarks>
    /// Not white: the page it floats over is white, so a white panel would be visible only by its border.
    /// It matches the theme's chrome instead, which is the colour a reader already reads as "not the
    /// document".
    /// </remarks>
    public static MarkerTipPalette Light { get; } = Create(
        Paint("#F3F3F3"),
        Paint("#C8C8C8"),
        Paint("#1E1E1E"));

    /// <summary>The palette for a dark background.</summary>
    /// <remarks>
    /// A step above the dark page for the same reason the light one is a step below the light page.
    /// </remarks>
    public static MarkerTipPalette Dark { get; } = Create(
        Paint("#2D2D2D"),
        Paint("#454549"),
        Paint("#D4D4D4"));

    /// <summary>What is painted behind the text.</summary>
    public IBrush Background { get; }

    /// <summary>The line around the box, which is what separates it from the document behind it.</summary>
    public IBrush Border { get; }

    /// <summary>The colour of the text.</summary>
    public IBrush Foreground { get; }

    /// <summary>Builds a palette.</summary>
    /// <param name="background">What is painted behind the text.</param>
    /// <param name="border">The line around the box.</param>
    /// <param name="foreground">The colour of the text.</param>
    /// <returns>The palette.</returns>
    /// <exception cref="ArgumentNullException">A brush is <see langword="null"/>.</exception>
    public static MarkerTipPalette Create(IBrush background, IBrush border, IBrush foreground)
    {
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(border);
        ArgumentNullException.ThrowIfNull(foreground);

        return new MarkerTipPalette(background, border, foreground);
    }

    /// <inheritdoc />
    public override string ToString() => $"tip on {Background}";

    private static ImmutableSolidColorBrush Paint(string colour) => new(Color.Parse(colour));
}
