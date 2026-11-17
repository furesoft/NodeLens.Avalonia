using System.Diagnostics.CodeAnalysis;

namespace Projectional.Language;

/// <summary>
/// The fluent surface language authors use to declare notation. Every method is a pure construction
/// of a cell; nothing here touches a model.
/// </summary>
/// <remarks>
/// The split between inline and block builders is deliberate: <see cref="Line"/> cannot be nested in
/// a line and <see cref="Keyword"/> cannot stand where a block has to go, because the signatures do
/// not allow it. Authoring mistakes therefore fail to compile instead of failing at startup.
/// </remarks>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "The members are instance methods on purpose: notation reads as ed.Keyword(\"if\"), and a static surface would have to be called through the type name.")]
public sealed class NotationEditor
{
    public static NotationEditor Create() => new();

    public ConstantNotationCell Keyword(string text, TextEmphasis emphasis = TextEmphasis.None) =>
        new(text, ConstantKind.Keyword, emphasis);

    public ConstantNotationCell Punct(string text, TextEmphasis emphasis = TextEmphasis.None) =>
        new(text, ConstantKind.Punctuation, emphasis);

    public SpaceNotationCell Space => SpaceNotationCell.Instance;

    public PropertyNotationCell Property(string slot, TextEmphasis emphasis = TextEmphasis.None) =>
        new(slot, emphasis);

    public ReferenceNotationCell Reference(
        string slot,
        string unsetText = "<none>",
        TextEmphasis emphasis = TextEmphasis.None) =>
        new(slot, emphasis) { UnsetText = unsetText };

    public SingleChildNotationCell ChildInline(string slot, string emptyText = "<empty>") =>
        new(slot) { EmptyText = emptyText };

    /// <summary>Inline cells that are drawn only while <paramref name="slot"/> holds <paramref name="value"/>.</summary>
    /// <param name="slot">The property the condition reads.</param>
    /// <param name="value">The text it has to equal, in the type's stored spelling.</param>
    /// <param name="content">What to draw while it does.</param>
    public ConditionalNotationCell Conditional(
        string slot,
        string value,
        params ReadOnlySpan<InlineNotationCell> content) =>
        new(slot, value, content);

    public LineNotationCell Line(params ReadOnlySpan<InlineNotationCell> content) => new(content);

    public SequenceNotationCell Sequence(params ReadOnlySpan<BlockNotationCell> content) => new(content);

    public IndentNotationCell Indent(params ReadOnlySpan<BlockNotationCell> content) => new(content);

    /// <summary>Every child of <paramref name="slot"/>, on one line or one per line.</summary>
    /// <param name="slot">The child slot to draw.</param>
    /// <param name="emptyText">What the slot shows while it holds nothing.</param>
    /// <param name="spacing">The gap between two children, in spaces; only used in a line.</param>
    /// <param name="stacked">Whether each child starts on a new line.</param>
    public ChildNotationCell Children(
        string slot,
        string emptyText = "<empty>",
        int spacing = 1,
        bool stacked = false) =>
        new(slot) { EmptyText = emptyText, Spacing = spacing, Stacked = stacked };

    public CollectionNotationCell Collection(
        string slot,
        InlineNotationCell? separator = null,
        string emptyText = "<empty>",
        int spacing = 1) =>
        new(slot) { Separator = separator, EmptyText = emptyText, Spacing = spacing };

    public TableColumn Column(string slot, InlineNotationCell header, string emptyText = "<empty>") =>
        new(slot, header) { EmptyText = emptyText };

    public TableNotationCell Table(string slot, params ReadOnlySpan<TableColumn> columns) =>
        new(slot, columns);

    /// <summary>
    /// A table whose empty state says something more useful than the default placeholder.
    /// </summary>
    /// <remarks>
    /// The text is drawn in the first column of the affordance row an empty table shows, so a language
    /// that wants the reader to see "add a rule here" rather than "&lt;empty&gt;" says so once, here.
    /// </remarks>
    public TableNotationCell Table(string slot, string emptyText, params ReadOnlySpan<TableColumn> columns) =>
        new(slot, columns) { EmptyText = emptyText };
}
