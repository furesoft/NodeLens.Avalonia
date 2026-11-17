using Projectional.Core;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Toolkit;

/// <summary>Draws a model as text, with no window, no font and no toolkit.</summary>
/// <remarks>
/// <para>
/// This is the one entry point a language author cannot reach otherwise, and it is why this package exists. The
/// renderer itself is <c>Projectional.Projection.LayoutSnapshot</c>, which ADR 0010 keeps internal: a language must not
/// be able to build its own projection, but it must be able to <em>look</em> at the one it declared - that is how a
/// notation gets tested, and a language nobody can test is a language nobody writes.
/// </para>
/// <para>
/// So the projection layer treats this package the way Core treats <c>Projectional.Persistence</c>: as a friend with a
/// narrow, documented reason to see the inside. Nothing else of the layer is re-exported, and no new behaviour is
/// introduced - each method is the renderer's own call, in the renderer's own vocabulary.
/// </para>
/// <para>
/// A snapshot is a test tool, not a second renderer: it draws the layout it is given, so a notation that lays out badly
/// shows up as text a test can assert on. The width is a cut rather than a wrap, because the caller asked for that
/// width.
/// </para>
/// </remarks>
public static class LanguageSnapshot
{
    /// <summary>Renders one node exactly as its concept's notation draws it.</summary>
    /// <param name="node">The node to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the snapshot has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    /// <returns>The drawing, one line per line, without a trailing newline.</returns>
    public static string Render(
        Node node,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(notations);

        return LayoutSnapshot.Render(node, notations, width, choice);
    }

    /// <summary>Renders every root of <paramref name="model"/>, stacked in model order.</summary>
    /// <param name="model">The model to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the snapshot has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    /// <returns>The drawing, one line per line, without a trailing newline.</returns>
    public static string Render(
        Model model,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(notations);

        return LayoutSnapshot.Render(model, notations, width, choice);
    }
}