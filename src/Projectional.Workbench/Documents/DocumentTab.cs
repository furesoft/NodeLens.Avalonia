using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;

namespace Projectional.Workbench;

/// <summary>
/// One open model in the workbench: its session, the file it came from, and whether the two still
/// agree.
/// </summary>
/// <remarks>
/// <para>
/// The tab is the app's unit of "a document", and it is deliberately more than the session it wraps.
/// A session does not know where a model lives or when it was last written down; a tab does, which is
/// what makes the two questions the shell has to answer — "which file is this" and "is it modified" —
/// answerable from one place.
/// </para>
/// <para>
/// A tab is a <em>model</em>, not a root, because a session owns an undo history and a history listens
/// to its whole model. Two sessions on one model would each record the other's changes and both would
/// offer to undo them, so M1 allows one session per model and therefore one tab per model. A model
/// with several roots shows its first one; giving each root its own session needs a shared history
/// first.
/// </para>
/// <para>
/// Modification is derived, never stored: the tab remembers the revision the model had when it was
/// last written or read, and compares. That is why undoing back to the saved state makes the document
/// clean again without the app having to notice anything.
/// </para>
/// </remarks>
internal sealed class DocumentTab : IDisposable
{
    private long _savedRevision;
    private bool _disposed;

    /// <summary>Opens <paramref name="model"/> for editing in a new tab.</summary>
    /// <param name="model">The model to edit; it must have a root.</param>
    /// <param name="notations">The notation that projects the model's concepts.</param>
    /// <param name="measure">
    /// How text is measured. There is no default on purpose: the layout is expressed in the measure's
    /// units, so a tab opened with a different measure than the one its editor paints with would put
    /// every cell in the wrong place. A host passes its editor's <c>TextMeasure</c>.
    /// </param>
    /// <param name="filePath">The file the model was read from, or <see langword="null"/> if it is new.</param>
    /// <param name="emphasis">
    /// What decorates this model beyond its notation, or <see langword="null"/> for nothing. It is a
    /// property of the language rather than of the tab, so the caller passes it once on opening and the
    /// session asks it again on every rebuild.
    /// </param>
    /// <exception cref="InvalidOperationException">The model has no root to edit.</exception>
    internal DocumentTab(
        Model model,
        NotationRegistry notations,
        ITextMeasure measure,
        string? filePath = null,
        ICellEmphasis? emphasis = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(notations);
        ArgumentNullException.ThrowIfNull(measure);

        if (model.Roots.Count == 0)
        {
            throw new InvalidOperationException(
                "A model without a root has nothing to open; add a root before opening a tab for it.");
        }

        Model = model;
        FilePath = filePath;
        Session = new EditorSession(model.Roots[0], notations, measure, emphasis);

        // A document that has just been opened or created is by definition what the file holds. The
        // history has not recorded anything yet, so this is the state it starts in.
        _savedRevision = Session.History.Revision;
    }

    /// <summary>The model this tab edits.</summary>
    internal Model Model { get; }

    /// <summary>The session that edits it.</summary>
    internal EditorSession Session { get; }

    /// <summary>The root the session shows.</summary>
    internal Node Root => Session.Document;

    /// <summary>The file the model was read from, once it has been read or written.</summary>
    internal string? FilePath { get; private set; }

    /// <summary>What the tab strip shows.</summary>
    internal string Title => Model.Name ?? "Untitled";

    /// <summary>Whether the model has changed since it was last read from or written to its file.</summary>
    internal bool IsDirty => Session.History.Revision != _savedRevision;

    /// <summary>Records that the model now matches what is on disk.</summary>
    /// <param name="filePath">
    /// The file just written, or <see langword="null"/> to keep the current one (a save to the same
    /// place).
    /// </param>
    /// <remarks>
    /// Clearing the coalescing key makes the save an undo boundary: without it the next keystroke
    /// would join the step that has just been written.
    /// </remarks>
    internal void MarkSaved(string? filePath = null)
    {
        if (filePath is not null)
        {
            FilePath = filePath;
        }

        _savedRevision = Session.History.Revision;
        Session.History.CoalescingKey = null;
    }

    /// <summary>Turns the tab's session off. The model itself belongs to whoever opened it.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Session.Dispose();
    }
}
