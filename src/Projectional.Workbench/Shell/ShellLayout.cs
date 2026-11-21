using Avalonia.Controls;

using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;

namespace Projectional.Workbench;

/// <summary>
/// The window's areas as a docking layout: tools on the left and along the bottom, the documents in the middle.
/// </summary>
/// <remarks>
/// <para>
/// A tree of dockables is a model, so it is built here rather than written as markup - the window's markup holds the
/// docking control and nothing else. Which tools exist is decided once, at construction, from what the caller passes: a
/// panel the capabilities do not grant is not composed at all, which is the rule the window's menu composition states,
/// applied to the layout.
/// </para>
/// <para>
/// <b>A hidden tool is hidden by the layout, not by the window.</b> Dock keeps the dockables a user closed in the root's
/// hidden list, and restoring one puts it back where it was - which is exactly what "View &#x25B8; Panels brings it back"
/// means, without a visibility flag beside a tab strip that is still on screen.
/// </para>
/// </remarks>
internal sealed class ShellLayout
{
    /// <summary>How much of the window's width the tool column starts with.</summary>
    private const double LeftProportion = 0.22;

    /// <summary>How much of the middle's height the documents start with, the rest being the bottom tools.</summary>
    private const double DocumentsProportion = 0.7;

    private readonly Factory _factory = new();
    private readonly HashSet<IDockable> _hidden = [];

    /// <summary>Builds the layout for the panels that are not <see langword="null"/>.</summary>
    /// <param name="emptyState">What the document area shows while no document is open.</param>
    /// <param name="project">The project explorer's view, or <see langword="null"/> when the shell has no explorer.</param>
    /// <param name="outline">The model browser's view, or <see langword="null"/> when the shell has no browser.</param>
    /// <param name="output">The output panel's view, or <see langword="null"/> when the shell cannot run anything.</param>
    /// <param name="problems">The problems panel's view, or <see langword="null"/> when the shell reports nothing.</param>
    /// <param name="usages">The usages panel's view, or <see langword="null"/> when the shell cannot find usages.</param>
    /// <param name="terminal">The terminal tool's view, or <see langword="null"/> when the shell has no terminal.</param>
    /// <param name="createDocument">
    /// What the document strip's own new-document button does, or <see langword="null"/> to offer none.
    /// </param>
    internal ShellLayout(
        Control emptyState,
        Control? project,
        Control? outline,
        Control? output,
        Control? problems,
        Control? usages,
        Control? terminal = null,
        Func<IDockable?>? createDocument = null)
    {
        ArgumentNullException.ThrowIfNull(emptyState);

        Documents = (DocumentDock)_factory.CreateDocumentDock();
        Documents.Id = "Documents";
        Documents.Title = "Documents";
        Documents.EmptyContent = emptyState;
        Documents.CanCreateDocument = createDocument is not null;
        Documents.DocumentFactory = createDocument is null ? null : () => createDocument()!;
        Documents.Proportion = DocumentsProportion;

        // A dock that is allowed to collapse is removed from the layout when its last document closes, and then the
        // empty state it was given never shows: the area simply disappears and the neighbouring tool takes its width.
        // The document area is what the window is for, so it keeps its place whether or not anything is open.
        Documents.IsCollapsable = false;

        ToolDock? left = null;
        var leftTools = new List<IDockable>();

        if (project is not null)
        {
            Project = Tool("Project", project);
            leftTools.Add(Project);
        }

        if (outline is not null)
        {
            Outline = Tool("Outline", outline);
            leftTools.Add(Outline);
        }

        if (leftTools.Count > 0)
        {
            left = Dock("Left", Alignment.Left, LeftProportion);
            left.VisibleDockables = _factory.CreateList<IDockable>([.. leftTools]);
            left.ActiveDockable = leftTools[0];
        }

        ToolDock? bottom = null;
        var bottomTools = new List<IDockable>();

        if (output is not null)
        {
            Output = Tool("Output", output);
            bottomTools.Add(Output);
        }

        if (problems is not null)
        {
            Problems = Tool("Problems", problems);
            bottomTools.Add(Problems);
        }

        if (usages is not null)
        {
            Usages = Tool("Usages", usages);
            bottomTools.Add(Usages);
        }

        if (terminal is not null)
        {
            Terminal = Tool("Terminal", terminal);
            bottomTools.Add(Terminal);
        }

        if (bottomTools.Count > 0)
        {
            bottom = Dock("Bottom", Alignment.Bottom, 1 - DocumentsProportion);
            bottom.VisibleDockables = _factory.CreateList<IDockable>([.. bottomTools]);
            bottom.ActiveDockable = bottomTools[0];
        }

        var center = new List<IDockable> { Documents };

        if (bottom is not null)
        {
            center.Add(Splitter("CenterSplitter"));
            center.Add(bottom);
        }

        var middle = (ProportionalDock)_factory.CreateProportionalDock();
        middle.Id = "Middle";
        middle.Orientation = Orientation.Vertical;
        middle.VisibleDockables = _factory.CreateList<IDockable>([.. center]);

        // A root dock holds one container, and a splitter only means something inside a proportional dock: putting the
        // areas straight under the root leaves the layout with children it cannot arrange, and it shows one of them.
        var outer = (ProportionalDock)_factory.CreateProportionalDock();
        outer.Id = "Outer";
        outer.Orientation = Orientation.Horizontal;
        outer.Proportion = 1;

        if (left is null)
        {
            outer.VisibleDockables = _factory.CreateList<IDockable>(middle);
        }
        else
        {
            middle.Proportion = 1 - LeftProportion;
            outer.VisibleDockables = _factory.CreateList<IDockable>(left, Splitter("LeftSplitter"), middle);
        }

        var root = (RootDock)_factory.CreateRootDock();
        root.Id = "Root";
        root.Title = "Workbench";
        root.IsFocusableRoot = true;
        root.VisibleDockables = _factory.CreateList<IDockable>(outer);

        Root = root;

        _factory.InitLayout(root);
    }

    /// <summary>The factory the control needs to resolve what it draws.</summary>
    internal Factory Factory => _factory;

    /// <summary>The root of the layout.</summary>
    internal RootDock Root { get; }

    /// <summary>Where the open documents live.</summary>
    internal DocumentDock Documents { get; }

    /// <summary>The project explorer's tool, or <see langword="null"/> when it was not composed.</summary>
    internal Tool? Project { get; private set; }

    /// <summary>The model browser's tool, or <see langword="null"/> when it was not composed.</summary>
    internal Tool? Outline { get; private set; }

    /// <summary>The output panel's tool, or <see langword="null"/> when it was not composed.</summary>
    internal Tool? Output { get; private set; }

    /// <summary>The problems panel's tool, or <see langword="null"/> when it was not composed.</summary>
    internal Tool? Problems { get; private set; }

    /// <summary>The usages panel's tool, or <see langword="null"/> when it was not composed.</summary>
    internal Tool? Usages { get; private set; }

    /// <summary>The terminal tool, or <see langword="null"/> when it was not composed.</summary>
    internal Tool? Terminal { get; private set; }

    /// <summary>Opens <paramref name="content"/> in a document of its own and brings it to the front.</summary>
    /// <param name="title">What the tab says.</param>
    /// <param name="content">What the tab shows.</param>
    internal Document Open(string title, Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var document = (Document)_factory.CreateDocument();
        document.Id = Guid.NewGuid().ToString("N");
        document.Title = title;
        document.Content = content;
        document.CanClose = true;

        Documents.AddDocument(document);
        Activate(document);

        return document;
    }

    /// <summary>Brings <paramref name="dockable"/> to the front.</summary>
    /// <param name="dockable">The document or tool to activate.</param>
    internal void Activate(IDockable dockable)
    {
        ArgumentNullException.ThrowIfNull(dockable);

        _factory.SetActiveDockable(dockable);
        _factory.SetFocusedDockable(Documents, dockable);
    }

    /// <summary>Takes <paramref name="document"/> out of the layout.</summary>
    /// <param name="document">The document to remove.</param>
    /// <remarks>
    /// Removed rather than closed: closing is what raises Dock's own closing event, and by the time the shell calls this
    /// the user has already answered the question the shell asked.
    /// </remarks>
    internal void Remove(IDockable document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _factory.RemoveDockable(document, collapse: false);
    }

    /// <summary>Whether <paramref name="tool"/> is on screen.</summary>
    /// <param name="tool">The tool to ask about, or <see langword="null"/> for one that does not exist.</param>
    internal bool IsShown(Tool? tool) => tool is not null && !_hidden.Contains(tool);

    /// <summary>Shows or puts away <paramref name="tool"/>.</summary>
    /// <param name="tool">The tool, or <see langword="null"/> for one this layout does not have.</param>
    /// <param name="visible">Whether it should be on screen.</param>
    internal void Show(Tool? tool, bool visible)
    {
        if (tool is null || visible == IsShown(tool))
        {
            return;
        }

        if (visible)
        {
            _hidden.Remove(tool);
            _factory.RestoreDockable(tool);
            Activate(tool);
        }
        else
        {
            _hidden.Add(tool);
            _factory.HideDockable(tool);
        }
    }

    /// <summary>Notes that Dock itself closed <paramref name="tool"/>, so the layout agrees with what is on screen.</summary>
    /// <param name="tool">The tool Dock closed, or <see langword="null"/> when it closed something else.</param>
    internal void NoteClosed(Tool? tool)
    {
        if (tool is not null)
        {
            _hidden.Add(tool);
        }
    }

    /// <summary>A splitter between two areas of the layout.</summary>
    private IDockable Splitter(string id)
    {
        var splitter = _factory.CreateProportionalDockSplitter();
        splitter.Id = id;

        return splitter;
    }

    /// <summary>A tool dock at one edge, ready for the tools its caller adds.</summary>
    private ToolDock Dock(string id, Alignment alignment, double proportion)
    {
        var dock = (ToolDock)_factory.CreateToolDock();
        dock.Id = id;
        dock.Title = id;
        dock.Alignment = alignment;
        dock.Proportion = proportion;
        dock.GripMode = GripMode.Visible;

        return dock;
    }

    /// <summary>A tool whose content is <paramref name="view"/>.</summary>
    private Tool Tool(string title, Control view)
    {
        var tool = (Tool)_factory.CreateTool();
        tool.Id = title;
        tool.Title = title;
        tool.Content = view;
        tool.CanClose = true;

        return tool;
    }
}
