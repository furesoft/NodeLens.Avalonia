using Projectional.Core;
using Projectional.Language;

namespace Projectional.Editors;

/// <summary>
/// The editor language as ordinary declarations: how a concept is drawn, written as a model.
/// </summary>
/// <remarks>
/// <para>
/// This is the second half of "a language is data" (slice P2). It is a separate language from
/// <c>Projectional.Structures</c> on purpose - a definition that mixes "what a rule is" with "where its keyword
/// sits" cannot be reviewed, which is why MPS keeps <c>structure.mps</c> and <c>editor.mps</c> apart.
/// </para>
/// <para>
/// The type split of the notation layer is carried by these declarations rather than by the lowering: a
/// <see cref="EditorConceptKeys.Line"/> holds <see cref="EditorConceptKeys.InlineCell"/>s and a
/// <see cref="EditorConceptKeys.Sequence"/> holds <see cref="EditorConceptKeys.BlockCell"/>s, so a definition
/// that puts a block inside a line is refused by the model. Nothing has to remember the rule twice.
/// </para>
/// </remarks>
public static class EditorLanguage
{
    /// <summary>The notation's root block.</summary>
    public const string RootSlot = "root";

    /// <summary>The cells a block stacks.</summary>
    public const string CellSlot = "cells";

    /// <summary>The columns of a table, left to right.</summary>
    public const string ColumnSlot = "columns";

    /// <summary>The key of the concept a notation draws.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The concept's declarations.</summary>
    /// <remarks>
    /// A notation is a document of its own: its root <em>is</em> the notation for one concept and one variant, so a
    /// language's editor aspect is the set of its editor documents. There is no container concept, for the same
    /// reason the structure language has none.
    /// </remarks>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            EditorConceptKeys.Notation,
            [
                new PropertySlot(
                    ConceptKeySlot,
                    PropertyType.String,
                    description: "The key of the concept this notation draws.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "variant",
                    PropertyType.String,
                    Notation.DefaultVariant,
                    "Which of the concept's notations this is. Every concept has a default one.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(RootSlot, EditorConceptKeys.BlockCell, Cardinality.One),
            ],
            nameSlot: ConceptKeySlot,
            description: "How one concept is drawn, in one variant."),

        new ConceptDeclaration(
            EditorConceptKeys.Cell,
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "Anything the notation draws."),

        new ConceptDeclaration(
            EditorConceptKeys.InlineCell,
            superConcepts: [EditorConceptKeys.Cell],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "A cell that runs inside a line."),

        new ConceptDeclaration(
            EditorConceptKeys.BlockCell,
            superConcepts: [EditorConceptKeys.Cell],
            isAbstract: true,
            isOfferedInCompletion: false,
            description: "A cell that occupies whole lines."),

        new ConceptDeclaration(
            EditorConceptKeys.Line,
            [new ChildSlot(CellSlot, EditorConceptKeys.InlineCell, Cardinality.Many)],
            [EditorConceptKeys.BlockCell],
            description: "One horizontal line of inline cells."),

        new ConceptDeclaration(
            EditorConceptKeys.Sequence,
            [new ChildSlot(CellSlot, EditorConceptKeys.BlockCell, Cardinality.Many)],
            [EditorConceptKeys.BlockCell],
            description: "Blocks stacked without indentation."),

        new ConceptDeclaration(
            EditorConceptKeys.Indent,
            [new ChildSlot(CellSlot, EditorConceptKeys.BlockCell, Cardinality.Many)],
            [EditorConceptKeys.BlockCell],
            description: "Blocks stacked one level deeper."),

        // The one block container that does not stack: the same children a sequence holds, in a row. It exists
        // because a notation whose cells are each a little diagram - an editor's `[`, its cells, its `]` - has to put
        // those blocks beside one another, and neither a line (inline cells only) nor a sequence (vertical) can say
        // that.
        new ConceptDeclaration(
            EditorConceptKeys.Horizontal,
            [new ChildSlot(CellSlot, EditorConceptKeys.BlockCell, Cardinality.Many)],
            [EditorConceptKeys.BlockCell],
            description: "Blocks arranged side by side instead of stacked."),

        new ConceptDeclaration(
            EditorConceptKeys.Space,
            superConcepts: [EditorConceptKeys.InlineCell],
            description: "A gap."),

        new ConceptDeclaration(
            EditorConceptKeys.TypeOf,
            superConcepts: [EditorConceptKeys.InlineCell],
            description: "The type of the node this notation draws, shown as an annotation."),

        // A picture is a key and a box: the notation names what it is, and the host owns the artwork, so a language
        // written as a project draws its icons without carrying a binary asset. `width`/`height` are layout units and
        // zero means "as tall as the line", which is what an inline icon wants - the box follows the font instead of
        // pinning a size the notation would have to guess.
        new ConceptDeclaration(
            EditorConceptKeys.Image,
            [
                new PropertySlot(
                    "key",
                    PropertyType.String,
                    description: "The name of the picture, which the host resolves - never a path.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "width",
                    PropertyType.Integer,
                    "0",
                    "How wide the box is, in layout units; zero means as tall as the line.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "height",
                    PropertyType.Integer,
                    "0",
                    "How tall the box is, in layout units; zero means as tall as the line.",
                    showDescriptionWhenFilled: true),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "key",
            description: "A picture on the line, drawn by the host from a key."),

        new ConceptDeclaration(
            EditorConceptKeys.Keyword,
            [
                new PropertySlot(
                    "text",
                    PropertyType.String,
                    description: "The word the notation itself draws, such as if or rule.",
                    showDescriptionWhenFilled: true),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "text",
            description: "A word the notation itself provides."),

        new ConceptDeclaration(
            EditorConceptKeys.Punct,
            [
                new PropertySlot(
                    "text",
                    PropertyType.String,
                    description: "The symbol the notation itself draws, such as a comma or a brace.",
                    showDescriptionWhenFilled: true),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "text",
            description: "A symbol the notation itself provides."),

        new ConceptDeclaration(
            EditorConceptKeys.Property,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the property slot whose value this cell draws.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "emptyText",
                    PropertyType.String,
                    description: "What the cell shows while the property holds no value. Display only, never a value.",
                    showDescriptionWhenFilled: true),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "The text of a property."),

        new ConceptDeclaration(
            EditorConceptKeys.Reference,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the reference slot whose target this cell draws.",
                    showDescriptionWhenFilled: true),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "The name of a reference's target."),

        new ConceptDeclaration(
            EditorConceptKeys.ChildInline,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the single child slot this cell draws.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "emptyText",
                    PropertyType.String,
                    "<empty>",
                    "What the cell shows while the slot is empty. Never nothing: the caret needs a place.",
                    showDescriptionWhenFilled: true),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "A single child, drawn inline."),

        new ConceptDeclaration(
            EditorConceptKeys.Children,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the child slot whose children this cell lists.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "emptyText",
                    PropertyType.String,
                    "<empty>",
                    "What the cell shows while the slot holds no children.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "spacing",
                    PropertyType.Integer,
                    "1",
                    "How many spaces separate one child from the next.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "stacked",
                    PropertyType.Boolean,
                    "false",
                    "Whether every child starts on a line of its own instead of beside its neighbours.",
                    showDescriptionWhenFilled: true),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Many children, drawn as blocks."),

        // Inline, and declared after the block cells on purpose: `abstract` is a word inside the concept's header
        // line, so the only place a conditional can stand today is inside a line.
        new ConceptDeclaration(
            EditorConceptKeys.Conditional,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the property whose value decides whether these cells are drawn.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "value",
                    PropertyType.String,
                    description: "The value that property has to hold for the cells to be drawn.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(CellSlot, EditorConceptKeys.InlineCell, Cardinality.Many),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "Inline cells drawn only while a property holds a value."),

        new ConceptDeclaration(
            EditorConceptKeys.WhileFilled,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the child slot whose child decides whether these cells are drawn.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(CellSlot, EditorConceptKeys.InlineCell, Cardinality.Many),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "Inline cells drawn only while a child slot holds a child."),

        new ConceptDeclaration(
            EditorConceptKeys.Collection,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the child slot whose children this cell lists.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "emptyText",
                    PropertyType.String,
                    "<empty>",
                    "What the cell shows while the slot holds no children.",
                    showDescriptionWhenFilled: true),
                new ChildSlot("separator", EditorConceptKeys.InlineCell, Cardinality.One),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Many children with a separator between them, drawn as blocks."),

        // The inline sibling of the collection above, and the reason it is a second concept rather than a flag on the
        // first: a list belongs inside a line - a call's arguments, a record's fields - and the block/inline split is
        // this language's type hierarchy, not a setting. Without it a declared language could not write `f(a, b)`.
        new ConceptDeclaration(
            EditorConceptKeys.InlineCollection,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the child slot whose children this cell lists.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "emptyText",
                    PropertyType.String,
                    "<empty>",
                    "What the cell shows while the slot holds no children.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "spacing",
                    PropertyType.Integer,
                    "1",
                    "How many spaces follow the separator.",
                    showDescriptionWhenFilled: true),
                new ChildSlot("separator", EditorConceptKeys.InlineCell, Cardinality.One),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "Many children with a separator between them, drawn inside a line."),

        // The block sibling of the property conditional, for the same reason the while-filled cell has one: a group of
        // lines that should leave no row has to be absent from the tree, and the inline conditional cannot hold a
        // block. C# 10's file-scoped namespace is the case - `namespace A;` and `namespace A { ... }` are one concept
        // with one boolean, and only one of the two shapes exists in either state.
        new ConceptDeclaration(
            EditorConceptKeys.ConditionalBlock,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the property whose value decides whether these blocks are drawn.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "value",
                    PropertyType.String,
                    description: "The value that property has to hold for the blocks to be drawn.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(CellSlot, EditorConceptKeys.BlockCell, Cardinality.Many),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Blocks drawn only while a property holds a value."),
        // The block sibling of the while-filled cell above, and it exists for the same reason the inline one does: a
        // group of lines that should leave no row at all cannot be an empty block, because layout gives every
        // vertical cell at least one line. Without it a declared language could say `if (...) { }` but not `if (...)
        // { } else { }` - the asymmetry ADR 0014 refuses, since the hand-written languages do have it.
        new ConceptDeclaration(
            EditorConceptKeys.WhileFilledBlock,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the child slot the condition reads.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(CellSlot, EditorConceptKeys.BlockCell, Cardinality.Many),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Blocks drawn only while a child slot holds a child."),

        new ConceptDeclaration(
            EditorConceptKeys.Table,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of the child slot whose children are the rows.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "emptyText",
                    PropertyType.String,
                    "<empty>",
                    "What the one affordance row shows while there are no rows.",
                    showDescriptionWhenFilled: true),
                new ChildSlot(ColumnSlot, EditorConceptKeys.Column, Cardinality.Many),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Many children drawn as a grid: one row per child, one cell per column."),

        new ConceptDeclaration(
            EditorConceptKeys.Column,
            [
                new PropertySlot(
                    "slot",
                    PropertyType.String,
                    description: "The name of a slot of the row concept, which every row draws here.",
                    showDescriptionWhenFilled: true),
                new PropertySlot(
                    "emptyText",
                    PropertyType.String,
                    "<empty>",
                    "What a row's cell shows when the row has nothing in this slot.",
                    showDescriptionWhenFilled: true),
                new ChildSlot("header", EditorConceptKeys.InlineCell, Cardinality.One),
            ],
            nameSlot: "slot",
            description: "One column of a table: a slot of the row concept, under a header."),
    ];

    /// <summary>The registry those declarations build, validated once.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A project in which a notation can be written.</summary>
    public static Project NewProject() => new(Concepts);

    /// <summary>A model of the editor language, ready for one language's notation.</summary>
    /// <param name="name">The model's name, which is also the file name it is saved under.</param>
    public static Model NewModel(string name = "notation") => NewProject().NewModel(name);
}
