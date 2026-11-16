using Projectional.Core;
using Projectional.Projection;

namespace Projectional.Editing;

/// <summary>
/// A copy of the document, in a project of its own, that one edit is applied to and that is thrown away.
/// </summary>
/// <remarks>
/// <para>
/// The sandbox is how "show what this edit would do" is answered without a description per action: the real
/// action is applied to a real document, and the result is projected. It cannot drift from the applied edit,
/// because it *is* the applied edit - the sandbox opens a session over the copy, so the path a preview takes is
/// the path the user's click takes ([ADR 0017](../../../docs/adr/0017-preview-is-a-discarded-edit.md)).
/// </para>
/// <para>
/// Nothing of the live document is reachable from here. <see cref="EditorSession.Notations"/>,
/// <see cref="EditorSession.Measure"/>, <see cref="EditorSession.Emphasis"/> and the caret are copied so that
/// the ghost is the same projection the page would be, but the model, the journal and the version are the
/// copy's own - which is why a preview is not undoable: it never happened.
/// </para>
/// <para>
/// A preview is best effort, so neither <see cref="Open"/> nor <see cref="Apply"/> throws over an edit that
/// cannot be previewed. The documented failure is an action that captured live <see cref="Node"/> objects
/// instead of addresses: on a copy those bugs are real, and they are reported as "no preview" rather than as a
/// broken editor.
/// </para>
/// </remarks>
public sealed class PreviewSandbox : IDisposable
{
    /// <summary>How many nodes a document may have before it is too expensive to preview.</summary>
    /// <remarks>
    /// The number is tuning, not contract; the contract is that a preview which would be too expensive is not
    /// shown at all, rather than shown late. Copying is linear in nodes, so this is what keeps a hover from
    /// costing seconds on a large document.
    /// </remarks>
    public const int NodeBudget = 20_000;

    private readonly Model _original;
    private readonly EditorSession _session;
    private bool _disposed;

    private PreviewSandbox(Model original, EditorSession session)
    {
        _original = original;
        _session = session;
    }

    /// <summary>The copy being edited, which is what a ghost draws.</summary>
    public Node Document => _session.Document;

    /// <summary>The projection of the copy.</summary>
    public EditorTree Tree => _session.Tree;

    /// <summary>The layout of that projection, in the session's own measure.</summary>
    public CellLayout Layout => _session.Layout;

    /// <summary>Where the caret stands in the copy, which starts where the live caret is.</summary>
    /// <remarks>
    /// Exposed because the ghost is anchored to the caret's line: a preview that moved the page's scroll
    /// position to show itself would answer the wrong question.
    /// </remarks>
    public Selection Selection => _session.Selection;

    /// <summary>What the last <see cref="Apply"/> found, or empty when nothing was applied.</summary>
    public IReadOnlyList<NodeDifference> Differences { get; private set; } = [];

    /// <summary>Opens a sandbox over a copy of <paramref name="session"/>'s document.</summary>
    /// <param name="session">The live session whose document is previewed.</param>
    /// <param name="nodeBudget">How many nodes the document may have, for tests to shrink.</param>
    /// <returns>
    /// The sandbox, or <see langword="null"/> when the document is over budget or cannot be copied - a preview
    /// that cannot be built is simply not shown.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The copy is taken of the whole *model*, not of the document's subtree, so references inside it keep
    /// resolving: the sandbox is a project, and a project is what a reference is resolved against.
    /// </remarks>
    public static PreviewSandbox? Open(EditorSession session, int nodeBudget = NodeBudget)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(nodeBudget);

        var original = session.Document.Model;

        if (original.Nodes.Skip(nodeBudget).Any())
        {
            return null;
        }

        try
        {
            var copy = original.CopyTo(new Project(original.Project.Concepts));

            if (copy.Project.FindNode(session.Document.Id) is not { } document)
            {
                return null;
            }

            var preview = new EditorSession(
                document,
                session.Notations,
                session.Measure,
                session.Emphasis,
                session.Choice,
                session.Clipboard,
                session.Typing,
                session.Triggers);

            // The caret is where the edit is about, so the copy starts where the user is looking. Addresses name
            // node ids, which the copy preserves, so the same selection resolves in the copy's own tree.
            preview.Select(session.Selection);

            return new PreviewSandbox(original, preview);
        }
        catch (Exception)
        {
            // Best effort by contract: a document that cannot be copied, or a projection that cannot be built,
            // costs the preview and nothing else.
            return null;
        }
    }

    /// <summary>Applies <paramref name="action"/> to the copy, exactly as the user's click would.</summary>
    /// <param name="action">The action the entry would run.</param>
    /// <returns>
    /// What the edit changed, or <see langword="null"/> when it could not be applied to the copy. An empty list
    /// is a different answer: the action ran and changed nothing.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The sandbox has been disposed.</exception>
    /// <remarks>
    /// More than one action may be applied and the differences then accumulate, but a preview uses one: the
    /// question it answers is about a single entry.
    /// </remarks>
    public IReadOnlyList<NodeDifference>? Apply(EditAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            _session.Apply(action);
        }
        catch (Exception)
        {
            // An action that throws on the copy is one that cannot be previewed - usually because it captured
            // live nodes instead of addresses. The copy is thrown away with the sandbox either way.
            return null;
        }

        Differences = ModelDiff.Of(_original, _session.Document.Model);

        return Differences;
    }

    /// <summary>Throws the copy away, with its session and its undo history.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Dispose();
    }
}
