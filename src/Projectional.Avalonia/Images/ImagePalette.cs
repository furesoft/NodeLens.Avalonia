using System.Collections.Frozen;

using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Projectional.Avalonia;

/// <summary>
/// Turns the name a notation gave a picture into the artwork that is drawn for it.
/// </summary>
/// <remarks>
/// <para>
/// The same split as <see cref="SyntaxPalette"/> and <see cref="CompletionPalette"/>, one layer down: the notation
/// says a picture <em>is</em> a key, and the host says what that key looks like. A key is never a path, so a
/// language keeps working when it is embedded in a host that stores its artwork elsewhere - the host simply maps the
/// same key to whatever it has.
/// </para>
/// <para>
/// The preset keys are drawn as vector paths, so the repository commits no binary asset and the icons stay sharp at
/// any scale and in either theme. A host with a real icon set passes <c>Bitmap</c>s instead; the control only ever
/// needs an <see cref="IImage"/> for the box the notation sized.
/// </para>
/// <para>
/// A key the palette does not know resolves to <see langword="null"/> on purpose. The control draws its own
/// placeholder for that case rather than the palette inventing one: a missing picture has to look missing in the
/// theme the document is read in, and the palette is not the thing that knows where the box is.
/// </para>
/// </remarks>
public sealed class ImagePalette
{
    /// <summary>The icon an error is marked with.</summary>
    public const string ErrorKey = "error";

    /// <summary>The icon a warning is marked with.</summary>
    public const string WarningKey = "warning";

    /// <summary>The icon a note is marked with.</summary>
    public const string InfoKey = "info";

    /// <summary>The icon a document is marked with.</summary>
    public const string DocumentKey = "document";

    /// <summary>The icon a key or an identifier is marked with.</summary>
    public const string KeyKey = "key";

    /// <summary>
    /// How thick a preset's outline is, in the 16-unit box its path is authored in.
    /// </summary>
    /// <remarks>
    /// A stroke and not a fill, so the thickness scales with the box the notation asked for: the same picture is
    /// drawn into an icon one line tall and into a picture many lines tall, and the line has to stay a line.
    /// </remarks>
    private const double StrokeWidth = 1.6;

    /// <summary>A cross, which reads as "wrong" in every icon set.</summary>
    private const string ErrorPath = "M 2,2 L 14,14 M 14,2 L 2,14";

    /// <summary>A triangle, which is the outline of a warning.</summary>
    private const string WarningPath = "M 8,1 L 15,13.5 L 1,13.5 Z";

    /// <summary>A filled circle, which is the outline of a note.</summary>
    private const string InfoPath = "M 8,1.5 A 6.5,6.5 0 1 0 8,14.5 A 6.5,6.5 0 1 0 8,1.5 Z";

    /// <summary>A page with its folded corner.</summary>
    private const string DocumentPath = "M 3,1.5 H 9.5 L 13,5 V 14.5 H 3 Z M 9.5,1.5 V 5 H 13";

    /// <summary>A bow with a straight shaft and two teeth, which is the shape a key is drawn as.</summary>
    /// <remarks>
    /// Authored for a 16-unit box at the size an inline icon is actually drawn at, which is about one line: a bow
    /// drawn as a full circle and a shaft with two short teeth stays legible there, while a detailed key does not.
    /// </remarks>
    private const string KeyPath = "M 3,8 A 3,3 0 1 0 9,8 A 3,3 0 1 0 3,8 M 9,8 H 14.5 M 11.5,8 V 10.5 M 13.5,8 V 10.5";

    private readonly FrozenDictionary<string, IImage> _pictures;

    private ImagePalette(FrozenDictionary<string, IImage> pictures) => _pictures = pictures;

    /// <summary>The palette for a light background.</summary>
    public static ImagePalette Light { get; } = Create(
    [
        Picture(ErrorKey, ErrorPath, "#C24444"),
        Picture(WarningKey, WarningPath, "#B8860B"),
        Picture(InfoKey, InfoPath, "#1E7FBF"),
        Picture(DocumentKey, DocumentPath, "#5A5A5A"),
        Picture(KeyKey, KeyPath, "#8A6D3B"),
    ]);

    /// <summary>The palette for a dark background.</summary>
    public static ImagePalette Dark { get; } = Create(
    [
        Picture(ErrorKey, ErrorPath, "#F17C7C"),
        Picture(WarningKey, WarningPath, "#E0C060"),
        Picture(InfoKey, InfoPath, "#7FC0EE"),
        Picture(DocumentKey, DocumentPath, "#C4C4C4"),
        Picture(KeyKey, KeyPath, "#D6BA80"),
    ]);

    /// <summary>Builds a palette from the pictures a host has.</summary>
    /// <param name="pictures">The artwork, keyed by the name a notation asks for.</param>
    /// <returns>The palette.</returns>
    /// <exception cref="ArgumentNullException">An entry is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A key is empty or mentioned twice.</exception>
    public static ImagePalette Create(IEnumerable<KeyValuePair<string, IImage>> pictures)
    {
        ArgumentNullException.ThrowIfNull(pictures);

        var built = new Dictionary<string, IImage>(StringComparer.Ordinal);

        foreach (var picture in pictures)
        {
            ArgumentException.ThrowIfNullOrEmpty(picture.Key);
            ArgumentNullException.ThrowIfNull(picture.Value);

            if (!built.TryAdd(picture.Key, picture.Value))
            {
                throw new ArgumentException($"The palette mentions the picture '{picture.Key}' twice.", nameof(pictures));
            }
        }

        return new ImagePalette(built.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>The artwork for <paramref name="key"/>, or <see langword="null"/> when the host has none.</summary>
    /// <param name="key">The name the notation gave the picture.</param>
    /// <returns>The picture, or <see langword="null"/> - which the control draws as a placeholder.</returns>
    public IImage? ImageOf(string key) =>
        _pictures.TryGetValue(key, out var picture) ? picture : null;

    /// <summary>Whether the host can draw <paramref name="key"/>.</summary>
    /// <param name="key">The name the notation gave the picture.</param>
    public bool Knows(string key) => _pictures.ContainsKey(key);

    public override string ToString() => $"image palette ({_pictures.Count} picture(s))";

    /// <summary>One vector picture, drawn in a box that starts at the origin so it scales into any box.</summary>
    /// <remarks>
    /// Every preset is a stroke rather than a filled shape: the paths are the open outlines an icon set is written
    /// in, and filling one would paint the area between its ends instead of the line they describe - a key's shaft
    /// closed into a wedge, a cross into nothing at all. The picture is fitted rather than stretched, so an outline
    /// authored in a wide, flat bounding box still reads as itself in the square box an inline icon gets.
    /// </remarks>
    private static KeyValuePair<string, IImage> Picture(string key, string path, string colour) =>
        new(
            key,
            new FitImage(new GeometryImage(StreamGeometry.Parse(path), pen: new ImmutablePen(Paint(colour), StrokeWidth))));

    private static ImmutableSolidColorBrush Paint(string colour) => new(Color.Parse(colour));
}
