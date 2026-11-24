using Projectional.Core;
using Projectional.Editing;
using Projectional.Persistence;

namespace Projectional.Web;

/// <summary>The documents the browser shell has open, in tab order, and which one is in front.</summary>
/// <remarks>
/// <para>
/// The shell's state, minus the page: the list, the active tab, and the four gestures the user can make on them -
/// open, activate, close, save. Keeping it out of the component is what lets a test open two documents, close the
/// first and assert that the second became active without a browser, and it is why the Razor file has no
/// collection logic in it.
/// </para>
/// <para>
/// A workspace owns its documents' sessions and disposes them together. It is not shared between workspaces: one
/// window, one workspace.
/// </para>
/// </remarks>
public sealed class EditorWorkspace : IDisposable
{
    private readonly List<EditorDocument> _documents = [];
    private readonly IDocumentStorage _storage;
    private readonly IEditorClipboard _clipboard;

    /// <summary>Creates a workspace that persists into <paramref name="storage"/>.</summary>
    /// <param name="storage">Where documents are saved and restored from.</param>
    /// <param name="clipboard">
    /// The clipboard every document of this workspace shares, or <see langword="null"/> for one of the workspace's
    /// own. Sharing it is what makes a copy in one tab pasteable in another; a host that has a platform clipboard
    /// passes its adapter here.
    /// </param>
    public EditorWorkspace(IDocumentStorage storage, IEditorClipboard? clipboard = null)
    {
        ArgumentNullException.ThrowIfNull(storage);

        _storage = storage;
        _clipboard = clipboard ?? new InMemoryClipboard();
    }

    /// <summary>The clipboard every document of this workspace shares.</summary>
    public IEditorClipboard Clipboard => _clipboard;

    /// <summary>The open documents, in tab order.</summary>
    public IReadOnlyList<EditorDocument> Documents => _documents;

    /// <summary>The document in front, or <see langword="null"/> when the workspace is empty.</summary>
    public EditorDocument? Active { get; private set; }

    /// <summary>Why the last open or restore failed, or <see langword="null"/> when it did not.</summary>
    public string? LastMessage { get; private set; }

    /// <summary>Opens a new document in <paramref name="language"/> and makes it the active one.</summary>
    /// <param name="language">The language to open it in.</param>
    /// <param name="sample">Whether the worked example is wanted instead of the empty shape.</param>
    /// <param name="name">The name to give it, or <see langword="null"/> for one derived from the language.</param>
    public EditorDocument New(WebLanguage language, bool sample = false, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(language);

        var model = language.NewModel(sample);
        var document = new EditorDocument(
            Guid.NewGuid().ToString("N"),
            Unique(name ?? (sample ? $"{language.Name} example" : $"Untitled {language.Name}")),
            language,
            model,
            _clipboard);

        _documents.Add(document);
        Active = document;
        LastMessage = null;

        return document;
    }

    /// <summary>Opens a stored document in <paramref name="language"/>.</summary>
    /// <param name="xml">The document's stored form.</param>
    /// <param name="language">The language it is read into.</param>
    /// <param name="name">What the tab shows.</param>
    /// <exception cref="ModelFormatException">The text is not a model this build can read.</exception>
    public EditorDocument OpenXml(string xml, WebLanguage language, string name)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var model = ModelXml.Deserialize(language.NewProject(), xml);
        var document = new EditorDocument(Guid.NewGuid().ToString("N"), Unique(name), language, model, _clipboard);

        _documents.Add(document);
        Active = document;
        LastMessage = null;

        return document;
    }

    /// <summary>Makes <paramref name="document"/> the one in front.</summary>
    /// <param name="document">A document this workspace holds.</param>
    /// <returns>Whether it was found.</returns>
    public bool Activate(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!_documents.Contains(document))
        {
            return false;
        }

        Active = document;

        // The tab in front is part of what a reload should show, and it changes without an edit.
        _storage.Write(DocumentStore.ActiveKey, document.Id);

        return true;
    }

    /// <summary>Closes <paramref name="document"/> and activates its neighbour.</summary>
    /// <param name="document">The document to close.</param>
    /// <returns>Whether it was closed; a document this workspace does not hold is not.</returns>
    /// <remarks>
    /// The neighbour is the one to the left, and when there is none the one that took its place. Closing the last
    /// tab therefore leaves no active document at all, which is the empty state the page shows - not a workspace
    /// that silently keeps a session nobody can see.
    /// </remarks>
    public bool Close(EditorDocument document)
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

        Persist();

        return true;
    }

    /// <summary>Gives <paramref name="document"/> a new name, keeping its identity and its text.</summary>
    /// <param name="document">The document to rename.</param>
    /// <param name="name">The name to give it; an empty one keeps the old.</param>
    public void Rename(EditorDocument document, string name)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        document.Name = Unique(name.Trim(), document);
        Persist();
    }

    /// <summary>Writes every document and the index, without claiming anything is saved.</summary>
    /// <remarks>
    /// This is the workspace's autosave: the page calls it after every change so that a reload loses nothing, and
    /// the dirty dot still means "not saved by the user" rather than "not written down at all". The index goes last,
    /// so a write that stops half way never points at a document whose text is missing.
    /// </remarks>
    public void Persist() => Write(_documents);

    /// <summary>Writes only the document in front, plus the index, which is the autosave after one edit.</summary>
    /// <remarks>
    /// A keystroke touches one document, and serializing every tab after it would make the cost of typing grow
    /// with the number of tabs. The index is still rewritten, because a rename or a new tab changes which documents
    /// exist without being an edit to any of them.
    /// </remarks>
    public void PersistActive()
    {
        if (Active is { } document)
        {
            _storage.Write(DocumentStore.DocumentKey(document.Id), document.Xml);
        }

        _storage.Write(DocumentStore.IndexKey, Index());
        _storage.Write(DocumentStore.ActiveKey, Active?.Id ?? string.Empty);
    }

    /// <summary>Writes every document, and marks the active one as saved.</summary>
    /// <returns>The number of documents written.</returns>
    public int Save()
    {
        Write(_documents);
        Active?.MarkSaved();

        return _documents.Count;
    }

    /// <summary>Reads the saved workspace back, replacing nothing when there is none.</summary>
    /// <returns>The number of documents restored.</returns>
    public int Restore()
    {
        var index = DocumentStore.DecodeIndex(_storage.Read(DocumentStore.IndexKey));

        if (index.Count == 0)
        {
            return 0;
        }

        Dispose();

        var refusal = default(string);

        foreach (var saved in index)
        {
            var xml = _storage.Read(DocumentStore.DocumentKey(saved.Id));

            if (xml is null)
            {
                refusal ??= $"'{saved.Name}' is in the index but its text is missing.";
                continue;
            }

            try
            {
                var language = WebLanguages.ByName(saved.Language);
                var model = ModelXml.Deserialize(language.NewProject(), xml);
                var document = new EditorDocument(saved.Id, saved.Name, language, model, _clipboard);

                _documents.Add(document);

                // The revision a restored session starts at is 0, and 0 is what a document that was loaded and not
                // touched reports. A restored document is therefore not dirty, which is what the user expects.
                document.MarkSaved();
            }
            catch (ModelFormatException exception)
            {
                refusal ??= $"'{saved.Name}' could not be read: {exception.Message}";
            }
        }

        Active = _documents.Count > 0
            ? _documents.FirstOrDefault(document => document.Id == _storage.Read(DocumentStore.ActiveKey)) ?? _documents[0]
            : null;
        LastMessage = refusal;

        return _documents.Count;
    }

    public void Dispose()
    {
        foreach (var document in _documents)
        {
            document.Dispose();
        }

        _documents.Clear();
        Active = null;
    }

    private void Write(IReadOnlyList<EditorDocument> documents)
    {
        foreach (var document in documents)
        {
            _storage.Write(DocumentStore.DocumentKey(document.Id), document.Xml);
        }

        _storage.Write(DocumentStore.IndexKey, Index());
        _storage.Write(DocumentStore.ActiveKey, Active?.Id ?? string.Empty);
    }

    private string Index() =>
        DocumentStore.EncodeIndex(
            _documents.Select(document => new SavedDocument(document.Id, document.Name, document.Language.Name)));

    /// <summary>A name no other tab uses, so two documents never share a download file or a menu row.</summary>
    private string Unique(string name, EditorDocument? allowed = null)
    {
        var candidate = name;
        var number = 2;

        while (_documents.Any(document => !ReferenceEquals(document, allowed) && string.Equals(document.Name, candidate, StringComparison.Ordinal)))
        {
            candidate = $"{name} {number++}";
        }

        return candidate;
    }
}
