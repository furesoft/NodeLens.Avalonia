using Projectional.Languages;

namespace Projectional.Workbench;

/// <summary>One row of the project explorer: a heading, a project, a folder, or a file.</summary>
/// <remarks>
/// A row is a value, and the tree's expansion state is not in it: whether a node is open belongs to the shell's view
/// state, so <see cref="IsExpanded"/> is what the builder was told, not something a row changes. Pressing a chevron
/// rebuilds the rows with one key flipped, which keeps the "derive the rows, leave the window as wiring" rule the
/// outline and the panels follow - and means a rebuilt list can never disagree with the tree.
/// </remarks>
internal sealed record ProjectEntry
{
    /// <summary>How far one level of nesting indents a row, in pixels.</summary>
    private const double IndentStep = 14;

    /// <summary>Creates a row.</summary>
    /// <param name="text">What the row says.</param>
    /// <param name="depth">How deep it sits, which is how far it is indented.</param>
    /// <param name="kind">What it is, which decides the icon and whether a chevron is drawn.</param>
    /// <param name="key">What the row is expanded or collapsed by, or <see langword="null"/> for a leaf.</param>
    /// <param name="isExpanded">Whether the row was drawn open.</param>
    /// <param name="document">The open document the row stands for, if any.</param>
    /// <param name="path">The file the row stands for, if any.</param>
    /// <param name="document">The open document the row stands for, if any.</param>
    /// <param name="path">The file the row stands for, if any.</param>
    /// <param name="folder">The aspect the row belongs to, if any.</param>
    /// <param name="module">The language module the row belongs to, if any.</param>
    internal ProjectEntry(
        string text,
        int depth,
        ProjectNode kind,
        string? key = null,
        bool isExpanded = true,
        DocumentTab? document = null,
        string? path = null,
        string? folder = null,
        LanguageProject? module = null)
    {
        Text = text;
        Depth = depth;
        Kind = kind;
        Key = key;
        IsExpanded = isExpanded;
        Document = document;
        Path = path;
        Folder = folder;
        Module = module;
    }

    /// <summary>What the row says.</summary>
    internal string Text { get; }

    /// <summary>How deep the row sits.</summary>
    internal int Depth { get; }

    /// <summary>What the row is.</summary>
    internal ProjectNode Kind { get; }

    /// <summary>The key the row is expanded or collapsed by, or <see langword="null"/> for a leaf.</summary>
    /// <remarks>
    /// Keys are file and folder paths, so a rebuild after an edit finds the same node again; a heading's key is its own
    /// name, which is stable for the life of the window.
    /// </remarks>
    internal string? Key { get; }

    /// <summary>Whether the row was drawn open.</summary>
    internal bool IsExpanded { get; }

    /// <summary>The open document the row stands for, or <see langword="null"/> when it has no tab.</summary>
    internal DocumentTab? Document { get; }

    /// <summary>The file the row stands for, or <see langword="null"/> for a heading, a project or a folder.</summary>
    internal string? Path { get; }

    /// <summary>The aspect this row belongs to, which decides the mark and the colour it is drawn with.</summary>
    /// <remarks>
    /// A folder row carries its own name on disk; a file row carries the folder it sits in, because a definition is
    /// read as "a concept of the structure", not as "a file called concept.mps". It is a plain string so that a row
    /// can answer it without a rendering platform.
    /// </remarks>
    internal string? Folder { get; }

    /// <summary>The language module the row belongs to, or <see langword="null"/> for a heading and for a document whose language has no folder.</summary>
    /// <remarks>
    /// The row carries it because "which module is this" is a fact the tree already had in hand when it built the row,
    /// and a shell that had to work it out again would answer from the selection - which is a different question the
    /// moment the user right-clicks a row that is not the selected one.
    /// </remarks>
    internal LanguageProject? Module { get; }

    /// <summary>What the row is drawn with, or <see langword="null"/> when it is drawn with nothing.</summary>
    /// <remarks>
    /// A heading is a heading: it leads the rows below it and needs no icon. Everything else is drawn with the mark of
    /// its aspect - or, when the aspect is unknown, with the plain folder or page the row really is.
    /// </remarks>
    internal string? Icon => Kind switch
    {
        ProjectNode.Group => null,
        ProjectNode.Project => ProjectIcons.ProjectKey,
        ProjectNode.Folder => Folder ?? ProjectIcons.FolderKey,
        _ => Folder ?? ProjectIcons.FileKey,
    };

    /// <summary>The letter a file row is drawn as, or <see langword="null"/> when the row has a glyph instead.</summary>
    /// <remarks>
    /// A file says what kind of definition it holds - a concept, an editor, a rule - which is the one thing a folder
    /// name alone cannot say. The letter comes from a table of names, so a test can assert it without a window.
    /// </remarks>
    internal string? Badge => Kind is ProjectNode.File ? ProjectIcons.BadgeOf(Folder) : null;

    /// <summary>Whether the row is drawn with a glyph.</summary>
    internal bool HasIcon => Icon is not null && Badge is null;

    /// <summary>Whether the row is drawn with a letter badge instead of a glyph.</summary>
    internal bool HasBadge => Badge is not null;

    /// <summary>Whether the row is a heading or a project, which the tree draws heavier than its rows.</summary>
    internal bool IsBold => Kind is ProjectNode.Group or ProjectNode.Project;

    /// <summary>Whether the row can be opened and closed, which is what draws a chevron.</summary>
    internal bool HasChildren => Kind is ProjectNode.Group or ProjectNode.Project or ProjectNode.Folder;

    /// <summary>Whether the row is a folder, which is drawn with a folder icon.</summary>
    internal bool IsFolder => Kind is ProjectNode.Folder;

    /// <summary>Whether the row is a language project, which is drawn with its own icon.</summary>
    internal bool IsProject => Kind is ProjectNode.Project;

    /// <summary>Whether the row is a definition file, which is drawn with a page icon.</summary>
    internal bool IsFile => Kind is ProjectNode.File;

    /// <summary>Whether the row's document has changes that are not on disk.</summary>
    internal bool IsDirty => Document is { IsDirty: true };

    /// <summary>How far the row is indented, in pixels.</summary>
    internal double Indent => Depth * IndentStep;

    /// <summary>Whether the row is indented at all, so a spacer of width zero is not drawn.</summary>
    internal bool HasIndent => Depth > 0;

    /// <summary>The text alone, which is what a screen reader is given for a row.</summary>
    public override string ToString() => Text;
}