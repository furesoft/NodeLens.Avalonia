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

    /// <summary>Renders one node as classified runs, for a caller that wants to colour the picture.</summary>
    /// <param name="node">The node to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the picture has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    /// <param name="types">The language's type rules, which a <c>TypeOf</c> cell draws.</param>
    /// <param name="annotations">
    /// Whether a <c>TypeOf</c> cell draws anything at all. <see langword="true"/> by default, because the annotation is
    /// what tells an author their type rules are missing; a caller showing a model as an <em>example</em> turns it off,
    /// and the cell is not built rather than built empty - the punctuation in front of it belongs to the annotation.
    /// </param>
    /// <returns>The runs in the order they are drawn, each with its row, its column, its text and its kind.</returns>
    /// <remarks>
    /// The same drawing as <see cref="Render(Node, NotationRegistry, int, INotationChoice?)"/>, answered as pieces rather
    /// than as characters, because a caller that colours text needs to know where a keyword starts and where it ends -
    /// and must not re-derive either from the characters, which is how a highlighter drifts from its editor.
    /// </remarks>
    public static IReadOnlyList<SnapshotRun> Runs(
        Node node,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null,
        TypeRegistry? types = null,
        bool annotations = true)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(notations);

        return LayoutSnapshot.Runs(node, notations, width, choice, types, annotations);
    }

    /// <summary>Renders every root of <paramref name="model"/> as classified runs.</summary>
    /// <param name="model">The model to render.</param>
    /// <param name="notations">The notation that projects it.</param>
    /// <param name="width">How many columns the picture has.</param>
    /// <param name="choice">Which variant of each concept to draw; the default ones when omitted.</param>
    public static IReadOnlyList<SnapshotRun> Runs(
        Model model,
        NotationRegistry notations,
        int width,
        INotationChoice? choice = null,
        TypeRegistry? types = null,
        bool annotations = true)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(notations);

        return LayoutSnapshot.Runs(model, notations, width, choice, types, annotations);
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