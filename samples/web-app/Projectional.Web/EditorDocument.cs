using Projectional.Core;
using Projectional.Editing;
using Projectional.Intentions;
using Projectional.Persistence;
using Projectional.Projection;

namespace Projectional.Web;

/// <summary>One open document in the browser shell: its session, its name and whether it is saved.</summary>
/// <remarks>
/// <para>
/// The browser's counterpart of the desktop shell's tab, and it answers the same two questions the shell owes a
/// user - "which document is this" and "is it written down" - from one place. The editing itself is
/// <see cref="EditorSession"/>, unchanged: the browser does not own a caret, an undo history or a projection, it
/// owns the page those are painted on.
/// </para>
/// <para>
/// Modification is derived from the history's revision rather than tracked by hand, so undoing back to the saved
/// state makes a document clean again without this type noticing anything. That is the same rule the desktop tab
/// follows, and it is why the two shells can share a session and still agree about what "unsaved" means.
/// </para>
/// </remarks>
public sealed class EditorDocument : IDisposable
{
    private long _savedRevision;

    /// <summary>Opens <paramref name="model"/> for editing in <paramref name="language"/>.</summary>
    /// <param name="id">The identity the workspace stores this document under; it survives a rename.</param>
    /// <param name="name">What the tab shows and what a download is called.</param>
    /// <param name="language">The language the model is projected and checked with.</param>
    /// <param name="model">The model to edit; it must have a root.</param>
    /// <param name="clipboard">
    /// What copy, cut and paste read and write, or <see langword="null"/> for this document's own. A shell passes one
    /// adapter to every tab, because a copy in one document has to be pasteable in another.
    /// </param>
    /// <exception cref="InvalidOperationException">The model has no root to open.</exception>
    public EditorDocument(string id, string name, WebLanguage language, Model model, IEditorClipboard? clipboard = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(model);

        if (model.Roots.Count == 0)
        {
            throw new InvalidOperationException("A model without a root has nothing to open.");
        }

        Id = id;
        Name = name;
        Language = language;
        Model = model;

        Session = new EditorSession(
            model.Roots[0],
            language.Notations,
            emphasis: language.Emphasis,
            typing: language.Typing,
            types: language.Types,
            clipboard: clipboard);

        if (language.Scopes is { } scopes)
        {
            Session.Scopes = scopes;
        }

        Session.AddIntentions(StandardIntentions.Provider, IntentionSource.Host);

        foreach (var intentions in language.Intentions)
        {
            Session.AddIntentions(intentions, IntentionSource.Language);
        }

        RefreshProblems();
        MarkSaved();
    }

    /// <summary>The identity the workspace persists this document under.</summary>
    public string Id { get; }

    /// <summary>What the tab shows; a rename keeps the identity.</summary>
    public string Name { get; set; }

    /// <summary>The language the document is opened in.</summary>
    public WebLanguage Language { get; }

    /// <summary>The model being edited.</summary>
    public Model Model { get; }

    /// <summary>The session the browser paints.</summary>
    public EditorSession Session { get; }

    /// <summary>What the language found in the document, and the marks that go with it.</summary>
    public ProblemReport Problems { get; private set; } = ProblemReport.Empty;

    /// <summary>Why the last edit was refused, or <see langword="null"/> when it was not.</summary>
    public string? LastMessage { get; private set; }

    /// <summary>Whether the document changed since it was last saved.</summary>
    public bool IsDirty => Session.History.Revision != _savedRevision;

    /// <summary>The document's stored form, byte-stable exactly as <see cref="ModelXml"/> promises.</summary>
    public string Xml => ModelXml.Serialize(Model);

    /// <summary>How many nodes the document holds, which is what the status bar reports.</summary>
    public int NodeCount => Model.Nodes.Count();

    /// <summary>Applies one edit action and updates the panel and the message.</summary>
    /// <param name="action">The action to run.</param>
    public EditResult Apply(EditAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var result = Session.Apply(action);

        LastMessage = result.Reason;

        if (result.Changed)
        {
            RefreshProblems();
        }

        return result;
    }

    /// <summary>Feeds typed text through the same rule the desktop control uses.</summary>
    /// <param name="text">The characters that arrived.</param>
    public EditResult Type(string text) => Apply(KeyRouter.Type(Session, text));

    /// <summary>Routes one editor key through the session-aware router.</summary>
    /// <param name="key">The key the page translated.</param>
    public EditResult Press(EditorKey key) => Apply(KeyRouter.For(Session, key));

    /// <summary>Undoes one step, keeping the projection consistent.</summary>
    public bool Undo()
    {
        if (!Session.Undo())
        {
            return false;
        }

        RefreshProblems();

        return true;
    }

    /// <summary>Redoes one step, keeping the projection consistent.</summary>
    public bool Redo()
    {
        if (!Session.Redo())
        {
            return false;
        }

        RefreshProblems();

        return true;
    }

    /// <summary>Puts the caret at <paramref name="address"/>, which is what clicking a problem row does.</summary>
    /// <param name="address">The cell to stand in.</param>
    public void Select(CellAddress address) => Session.Select(Selection.At(address));

    /// <summary>Puts the caret on <paramref name="id"/>, which is what clicking a model-browser row does.</summary>
    /// <param name="id">The node to stand in.</param>
    public void SelectNode(NodeId id) => Session.Select(Selection.At(new CellAddress(id)));

    /// <summary>The node the caret's cell belongs to, or <see langword="null"/> before the caret is resolved.</summary>
    public NodeId? CaretNode => Session.Selection.Caret.Address.Owner;

    /// <summary>Selects the word under the caret, which is what a double click means.</summary>
    public void SelectWord() => Session.Apply(new SelectWordAction());

    /// <summary>Selects the whole value under the caret, which is what a triple click means.</summary>
    public void SelectValue() => Session.Apply(new SelectValueAction());

    /// <summary>Accepts the completion proposal at <paramref name="index"/>, for a click on a row.</summary>
    /// <param name="index">The row's index in the list that is on screen.</param>
    public EditResult AcceptCompletion(int index)
    {
        if (Session.Completion is not { } list || index < 0 || index >= list.Matches.Count)
        {
            return EditResult.Unchanged(Session.Selection);
        }

        // A click on the row that is already highlighted is the common case (the first row is selected when the
        // list opens), and `SelectCompletionAction` refuses a zero move - there is nowhere to move to.
        var delta = index - list.SelectedIndex;

        if (delta != 0)
        {
            Session.Apply(new SelectCompletionAction(delta));
        }

        return Apply(new AcceptCompletionAction());
    }

    /// <summary>The frame the page paints: every run, the highlights and the caret.</summary>
    /// <remarks>
    /// The viewport is the whole document rather than a window onto it, because the page's scroll container owns
    /// the scrolling: the browser's own bars are the shell's bars, and a frame that culled by a viewport nobody
    /// scrolled would hide text the user can reach.
    /// </remarks>
    public EditorFrame Frame()
    {
        var size = Session.Layout.Size;

        return EditorFrame.Build(
            Session.Layout,
            new LayoutSize(Math.Max(size.Width, 1), Math.Max(size.Height, 1)),
            0,
            0,
            Session.Selection,
            Session.Measure);
    }

    /// <summary>The document's size in layout units.</summary>
    public LayoutSize Extent => Session.Layout.Size;

    /// <summary>Marks the document's current revision as the saved one.</summary>
    public void MarkSaved() => _savedRevision = Session.History.Revision;

    /// <summary>Re-runs the language's rules, which a restored document needs once as well.</summary>
    public void RefreshProblems() => Problems = DocumentProblems.Of(Model, Language, Session.Scopes);

    /// <summary>Re-projects after a change the session did not make.</summary>
    public void Refresh()
    {
        Session.Refresh();
        RefreshProblems();
    }

    public void Dispose() => Session.Dispose();
}
