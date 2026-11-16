using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// One painted frame of a preview: the sandbox's text, what happened to each cell, and the badge.
/// </summary>
/// <remarks>
/// <para>
/// The ghost is drawn <em>instead of</em> the page rather than over it, because an overlay would draw the new
/// text at the old positions and lie about the layout - a rename changes widths. So the ghost is a frame of its
/// own, built from the sandbox's layout with the live page's viewport, which is also how it stays anchored: the
/// user keeps looking at the region they were looking at.
/// </para>
/// <para>
/// It carries no caret and no selection on purpose. A preview is not editable and must not look as if the live
/// document had a caret somewhere else; the host restores the real selection when the preview ends.
/// </para>
/// <para>
/// The one rule worth stating: <b>a cell with no mark is dimmed</b>. The marks are what the edit did, and dimming
/// everything else is what makes them read first - so "unchanged" is not a mark but the absence of one.
/// </para>
/// </remarks>
public sealed class GhostFrame
{
    private GhostFrame(
        LayoutViewport viewport,
        ITextMeasure measure,
        IReadOnlyList<TextRun> textRuns,
        PreviewMarks marks,
        string badge)
    {
        Viewport = viewport;
        Measure = measure;
        TextRuns = textRuns;
        Marks = marks;
        Badge = badge;
    }

    /// <summary>The slice of the sandbox's layout that is on screen, at the live page's offset.</summary>
    public LayoutViewport Viewport { get; }

    /// <summary>The measure the geometry was computed with.</summary>
    public ITextMeasure Measure { get; }

    /// <summary>The text to draw, in document order.</summary>
    public IReadOnlyList<TextRun> TextRuns { get; }

    /// <summary>What the edit did, which is what the marks on these runs come from.</summary>
    public PreviewMarks Marks { get; }

    /// <summary>The sentence the host puts in the corner, such as <c>Preview - rename, 4 changes</c>.</summary>
    public string Badge { get; }

    /// <summary>Builds the ghost of <paramref name="sandbox"/> for one viewport.</summary>
    /// <param name="sandbox">The sandbox whose edit has already been applied.</param>
    /// <param name="marks">What that edit did, resolved against the sandbox's tree.</param>
    /// <param name="label">What the entry being previewed is called, for the badge.</param>
    /// <param name="viewport">How much of the page fits on screen.</param>
    /// <param name="offsetX">The live page's horizontal scroll offset.</param>
    /// <param name="offsetY">The live page's vertical scroll offset.</param>
    /// <param name="styler">
    /// What each cell's text means, defaulting to <see cref="CellStyler.Default"/> - the same styler the page
    /// uses, or the ghost would recolour a document nobody edited.
    /// </param>
    /// <exception cref="ArgumentNullException">A required parameter is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="label"/> is blank.</exception>
    /// <remarks>
    /// The offsets are passed in rather than read from anywhere: the ghost is anchored to the line the user is
    /// looking at, and the only thing that knows where that is is the page.
    /// </remarks>
    public static GhostFrame Build(
        PreviewSandbox sandbox,
        PreviewMarks marks,
        string label,
        LayoutSize viewport,
        double offsetX,
        double offsetY,
        ICellStyler? styler = null)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(marks);

        var window = LayoutViewport.Of(sandbox.Layout, viewport, offsetX, offsetY);

        return new GhostFrame(
            window,
            sandbox.Measure,
            EditorFrame.TextRunsOf(window, sandbox.Layout, styler ?? CellStyler.Default, sandbox.Measure),
            marks,
            PreviewBadge.Of(label, marks));
    }

    /// <summary>What happened to the cell <paramref name="run"/> draws, or <see langword="null"/> if nothing.</summary>
    /// <param name="run">One of <see cref="TextRuns"/>.</param>
    public CellChange? ChangeOf(TextRun run) => Marks.ChangeAt(run.Address);

    /// <summary>Whether <paramref name="run"/> is drawn dimmed, which is what "unchanged" looks like.</summary>
    /// <param name="run">One of <see cref="TextRuns"/>.</param>
    public bool IsDimmed(TextRun run) => ChangeOf(run) is null;

    public override string ToString() =>
        $"{TextRuns.Count} runs, {Marks.Changes.Count} marks, '{Badge}' in {Viewport}";
}
