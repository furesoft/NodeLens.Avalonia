using Projectional.Core;

namespace Projectional.Workbench;

/// <summary>
/// The documents the workbench has open, in tab order, with one of them active.
/// </summary>
/// <remarks>
/// <para>
/// The set exists so that the window does not keep the list itself. Closing a tab has consequences the
/// window should not have to remember — the session has to be disposed, the active document has to
/// move to a neighbour, and the ordering of the strip has to survive — and those are rules, so they
/// are here, where a test can drive them without a window.
/// </para>
/// <para>
/// It owns the tabs it holds and disposes them, which is the same relationship the set has with the
/// models: it does not create them, but once a tab is closed its session is over.
/// </para>
/// </remarks>
internal sealed class DocumentSet : IDisposable
{
    private readonly List<DocumentTab> _documents = [];
    private bool _disposed;

    /// <summary>The open documents, in the order their tabs appear.</summary>
    internal IReadOnlyList<DocumentTab> Documents => _documents;

    /// <summary>The document whose tab is selected, or <see langword="null"/> when none is.</summary>
    internal DocumentTab? Active { get; private set; }

    /// <summary>How many documents are open.</summary>
    internal int Count => _documents.Count;

    /// <summary>Whether any open document has changes that are not on disk.</summary>
    internal bool AnyUnsaved
    {
        get
        {
            foreach (var document in _documents)
            {
                if (document.IsDirty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The documents that have changes not on disk, in tab order.</summary>
    internal IEnumerable<DocumentTab> Unsaved => _documents.FindAll(document => document.IsDirty);

    /// <summary>Whether <paramref name="model"/> already has a tab.</summary>
    /// <param name="model">The model to look for.</param>
    internal bool IsOpen(Model model) => Find(model) is not null;

    /// <summary>The tab that edits <paramref name="model"/>, or <see langword="null"/>.</summary>
    /// <param name="model">The model to look for.</param>
    internal DocumentTab? Find(Model model)
    {
        foreach (var document in _documents)
        {
            if (ReferenceEquals(document.Model, model))
            {
                return document;
            }
        }

        return null;
    }

    /// <summary>Adds <paramref name="document"/> at the end of the strip and makes it active.</summary>
    /// <param name="document">The document to open.</param>
    /// <exception cref="InvalidOperationException">Its model already has a tab.</exception>
    internal void Add(DocumentTab document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (IsOpen(document.Model))
        {
            throw new InvalidOperationException(
                "This model already has a tab; a second session on the same model would fight the first over the undo history.");
        }

        _documents.Add(document);
        Active = document;
    }

    /// <summary>Closes <paramref name="document"/>, disposing its session.</summary>
    /// <param name="document">The document to close.</param>
    /// <returns>Whether the document was open.</returns>
    /// <remarks>
    /// Closing the last document leaves nothing active. Closing any other hands the active flag to the
    /// tab that took its place, or to the one before it when the closed tab was last, which is what a
    /// user expects the strip to do.
    /// </remarks>
    internal bool Remove(DocumentTab document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var index = _documents.IndexOf(document);
        if (index < 0)
        {
            return false;
        }

        _documents.RemoveAt(index);
        document.Dispose();

        if (ReferenceEquals(Active, document))
        {
            Active = _documents.Count == 0 ? null : _documents[Math.Min(index, _documents.Count - 1)];
        }

        return true;
    }

    /// <summary>Makes <paramref name="document"/> the active one.</summary>
    /// <param name="document">An open document, or <see langword="null"/> to select none.</param>
    /// <exception cref="ArgumentException">The document is not open.</exception>
    internal void Activate(DocumentTab? document)
    {
        if (document is not null && !_documents.Contains(document))
        {
            throw new ArgumentException("Only an open document can be active.", nameof(document));
        }

        Active = document;
    }

    /// <summary>Closes every document and disposes their sessions.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var document in _documents)
        {
            document.Dispose();
        }

        _documents.Clear();
        Active = null;
    }
}
