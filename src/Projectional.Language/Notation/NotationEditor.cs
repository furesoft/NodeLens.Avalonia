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

    public ConstantNotationCell Keyword(string text) => new(text, ConstantKind.Keyword);

    public ConstantNotationCell Punct(string text) => new(text, ConstantKind.Punctuation);

    public SpaceNotationCell Space => SpaceNotationCell.Instance;

    public PropertyNotationCell Property(string slot) => new(slot);

    public ReferenceNotationCell Reference(string slot, string unsetText = "<none>") =>
        new(slot) { UnsetText = unsetText };

    public SingleChildNotationCell ChildInline(string slot, string emptyText = "<empty>") =>
        new(slot) { EmptyText = emptyText };

    public LineNotationCell Line(params ReadOnlySpan<InlineNotationCell> content) => new(content);

    public SequenceNotationCell Sequence(params ReadOnlySpan<BlockNotationCell> content) => new(content);

    public IndentNotationCell Indent(params ReadOnlySpan<BlockNotationCell> content) => new(content);

    public ChildNotationCell Children(string slot, string emptyText = "<empty>") =>
        new(slot) { EmptyText = emptyText };

    public CollectionNotationCell Collection(
        string slot,
        InlineNotationCell? separator = null,
        string emptyText = "<empty>") =>
        new(slot) { Separator = separator, EmptyText = emptyText };
}
