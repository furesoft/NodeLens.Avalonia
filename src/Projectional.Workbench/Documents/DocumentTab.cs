using Projectional.Avalonia;
using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;
using Projectional.Projection;
using Projectional.Semantics;

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
    /// <param name="typing">
    /// What the language says about text typed into an empty slot, or <see langword="null"/> when typing
    /// there should be ignored.
    /// </param>
    /// <param name="triggers">
    /// What the language says about text typed at the edge of a node that already exists, or
    /// <see langword="null"/> when such a keystroke should do nothing.
    /// </param>
    /// <param name="types">
    /// The language's type rules, or <see langword="null"/> when it declares none.
    /// </param>
    /// <param name="constraints">
    /// The rules the language states about itself, or <see langword="null"/> when the caller has none to add - which
    /// is what the sample language's own rules stand in for.
    /// </param>
    /// <param name="scopes">
    /// The scope providers the language declares, or <see langword="null"/> for the default chain.
    /// </param>
    /// <param name="evaluation">
    /// The language's interpreter - the rules that give its concepts a meaning - or <see langword="null"/> when it
    /// defines none, in which case the document cannot be run at all.
    /// </param>
    /// <param name="clipboard">
    /// What copy, cut and paste read and write, or <see langword="null"/> for a private in-memory clipboard.
    /// A shell passes one adapter to every tab, because a copy in one document has to be pasteable in another -
    /// and because the control refreshes the adapter it was given, so a session on a different one would paste
    /// text the platform has long replaced.
    /// </param>
    /// <exception cref="InvalidOperationException">The model has no root to edit.</exception>
    internal DocumentTab(
        Model model,
        NotationRegistry notations,
        ITextMeasure measure,
        string? filePath = null,
        ICellEmphasis? emphasis = null,
        ITypingRules? typing = null,
        TypeRegistry? types = null,
        ConstraintSet? constraints = null,
        IScopeProvider? scopes = null,
        EvaluationRegistry? evaluation = null,
        IReadOnlyList<TextGenerator>? generators = null,
        IEditorClipboard? clipboard = null)
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
        Types = types;
        Constraints = constraints;
        Evaluation = evaluation;
        Generators = generators ?? [];
        Session = new EditorSession(model.Roots[0], notations, measure, emphasis, typing: typing, clipboard: clipboard);

        // A language that declares scopes replaces the chain for the references it names; every other slot still
        // falls through to the default, which is what makes installing it harmless.
        Session.Scopes = scopes ?? DefaultScopeProvider.Instance;

        // A document that has just been opened or created is by definition what the file holds. The
        // history has not recorded anything yet, so this is the state it starts in.
        _savedRevision = Session.History.Revision;
    }

    /// <summary>The model this tab edits.</summary>
    internal Model Model { get; }

    /// <summary>
    /// The type rules of the language this document belongs to, or <see langword="null"/> when it is a
    /// document of the built-in sample.
    /// </summary>
    /// <remarks>
    /// The tab carries it because the findings are published per document: a document of an opened
    /// language must be checked with that language's rules, not with the sample's.
    /// </remarks>
    internal TypeRegistry? Types { get; }

    /// <summary>
    /// The rules the language of this document states about itself, or <see langword="null"/> when it declares none.
    /// </summary>
    /// <remarks>
    /// It travels with the tab for the same reason the type rules do: a document of a language opened from a folder
    /// must be checked with that language's rules, not with the sample's. <see langword="null"/> therefore means "this
    /// caller added none", which is how the sample language's own rules keep applying where they always did.
    /// </remarks>
    internal ConstraintSet? Constraints { get; }

    /// <summary>
    /// The interpreter of the language this document belongs to, or <see langword="null"/> when that language
    /// defines none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A language says what its nodes mean, so "can this document be run" is a fact about the language rather than
    /// about the document - which is why the registry travels with the tab instead of being picked when the user
    /// presses Run. A language with no interpreter is not runnable, and the shell refuses rather than running it
    /// against another language's rules and reporting a failure that is really a missing declaration.
    /// </para>
    /// <para>
    /// The default is <see langword="null"/> on purpose: a caller that forgets this aspect gets a document that
    /// cannot be run, never one that silently runs with the sample language's semantics.
    /// </para>
    /// </remarks>
    internal EvaluationRegistry? Evaluation { get; }

    /// <summary>The text generators of the language this document belongs to; empty when it declares none.</summary>
    /// <remarks>
    /// A list, not one generator: a language decides how many text targets it has, and the shell offers them by name.
    /// Deriving it here rather than at the command is the same rule the interpreter follows - what a document can do
    /// is a fact about its language.
    /// </remarks>
    internal IReadOnlyList<TextGenerator> Generators { get; }

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

    /// <summary>What the checks last found in this document, in document order.</summary>
    /// <remarks>
    /// Held on the tab rather than recomputed by the panel, so switching tabs shows each document's own findings
    /// instead of running every check again. It is a snapshot: the model's change event is what replaces it, which is
    /// the same rule the editor's marker set follows.
    /// </remarks>
    internal IReadOnlyList<ProblemEntry> Problems { get; set; } = [];

    /// <summary>What the last run of this document produced, or <see langword="null"/> when it has not been run.</summary>
    /// <remarks>
    /// Kept per tab so switching documents shows each one's own answer, and dropped when the model changes: a headline
    /// about a document that no longer exists is worse than no headline, and a run cannot be repeated on the user's
    /// behalf because the variables came from them.
    /// </remarks>
    internal RunReport? Output { get; set; }

    /// <summary>The text of the last generation, or <see langword="null"/> when nothing was generated.</summary>
    /// <remarks>
    /// Kept beside <see cref="Output"/> because the panel shows rows while a save writes the whole text: the report has
    /// been split into lines by then, and rebuilding it from the rows would be a second generator's job.
    /// </remarks>
    internal string? Generated { get; set; }

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
