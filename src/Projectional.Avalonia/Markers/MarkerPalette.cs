using Avalonia.Media;
using Avalonia.Media.Immutable;

using Projectional.Semantics;

namespace Projectional.Avalonia;

/// <summary>
/// Says what a severity looks like: one brush per severity, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// The same split as <see cref="SyntaxPalette"/>, one axis over: the editing layer decides which stretch
/// of text is stroked and which severity wins where two findings meet, and a palette decides what that
/// severity is painted in. Nothing here reaches the model, so swapping a palette is a repaint - the tree,
/// the layout and every rectangle stay exactly as they were.
/// </para>
/// <para>
/// Colour only. The wave is drawn by the control at a fixed amplitude and thickness, because the height it
/// has to fit into comes from the font, not from the theme; a palette that could change it would let a
/// host draw outside the line the layout reserved.
/// </para>
/// </remarks>
public sealed class MarkerPalette
{
    private MarkerPalette(IBrush error, IBrush warning, IBrush info, IBrush hint)
    {
        Error = error;
        Warning = warning;
        Info = info;
        Hint = hint;
    }

    /// <summary>The palette for a light background.</summary>
    public static MarkerPalette Light { get; } =
        Create(Paint("#E51400"), Paint("#BF8803"), Paint("#1A85FF"), Paint("#808080"));

    /// <summary>The palette for a dark background.</summary>
    public static MarkerPalette Dark { get; } =
        Create(Paint("#F14C4C"), Paint("#CCA700"), Paint("#75BEFF"), Paint("#A0A0A0"));

    /// <summary>The colour of a stretch that cannot mean what it says.</summary>
    public IBrush Error { get; }

    /// <summary>The colour of a stretch that is probably not what was intended.</summary>
    public IBrush Warning { get; }

    /// <summary>The colour of an observation that is not a defect.</summary>
    public IBrush Info { get; }

    /// <summary>The colour of a suggestion the user may ignore.</summary>
    public IBrush Hint { get; }

    /// <summary>Builds a palette.</summary>
    /// <param name="error">The colour of an error.</param>
    /// <param name="warning">The colour of a warning.</param>
    /// <param name="info">The colour of an observation.</param>
    /// <param name="hint">The colour of a suggestion.</param>
    /// <returns>The palette.</returns>
    /// <exception cref="ArgumentNullException">A brush is <see langword="null"/>.</exception>
    public static MarkerPalette Create(IBrush error, IBrush warning, IBrush info, IBrush hint)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(warning);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(hint);

        return new MarkerPalette(error, warning, info, hint);
    }

    /// <summary>The colour of <paramref name="severity"/>.</summary>
    /// <param name="severity">How much a finding matters.</param>
    /// <returns>The brush that severity is stroked in.</returns>
    public IBrush BrushOf(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => Error,
        DiagnosticSeverity.Warning => Warning,
        DiagnosticSeverity.Info => Info,
        _ => Hint,
    };

    /// <summary>This palette with one severity's colour changed.</summary>
    /// <param name="severity">The severity to recolour.</param>
    /// <param name="brush">The colour to draw it in.</param>
    /// <returns>A new palette; this one is unchanged, so a palette can be shared and amended safely.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="brush"/> is <see langword="null"/>.</exception>
    public MarkerPalette With(DiagnosticSeverity severity, IBrush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);

        return new MarkerPalette(
            severity == DiagnosticSeverity.Error ? brush : Error,
            severity == DiagnosticSeverity.Warning ? brush : Warning,
            severity == DiagnosticSeverity.Info ? brush : Info,
            severity == DiagnosticSeverity.Hint ? brush : Hint);
    }

    /// <inheritdoc />
    public override string ToString() => "4 marker colour(s)";

    private static ImmutableSolidColorBrush Paint(string colour) => new(Color.Parse(colour));
}
