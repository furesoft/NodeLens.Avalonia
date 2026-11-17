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
    /// <summary>The slot holding the notations of one language definition.</summary>
    public const string NotationSlot = "notations";

    /// <summary>The notation's root block.</summary>
    public const string RootSlot = "root";

    /// <summary>The cells a block stacks.</summary>
    public const string CellSlot = "cells";

    /// <summary>The columns of a table, left to right.</summary>
    public const string ColumnSlot = "columns";

    /// <summary>The key of the concept a notation draws.</summary>
    public const string ConceptKeySlot = "conceptKey";

    /// <summary>The concept's declarations.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } =
    [
        new ConceptDeclaration(
            EditorConceptKeys.Notations,
            [new ChildSlot(NotationSlot, EditorConceptKeys.Notation, Cardinality.Many)],
            description: "How one language is drawn: one notation per concept."),

        new ConceptDeclaration(
            EditorConceptKeys.Notation,
            [
                new PropertySlot(ConceptKeySlot, PropertyType.String),
                new PropertySlot("variant", PropertyType.String, Notation.DefaultVariant),
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

        new ConceptDeclaration(
            EditorConceptKeys.Space,
            superConcepts: [EditorConceptKeys.InlineCell],
            description: "A gap."),

        new ConceptDeclaration(
            EditorConceptKeys.Keyword,
            [new PropertySlot("text", PropertyType.String)],
            [EditorConceptKeys.InlineCell],
            nameSlot: "text",
            description: "A word the notation itself provides."),

        new ConceptDeclaration(
            EditorConceptKeys.Punct,
            [new PropertySlot("text", PropertyType.String)],
            [EditorConceptKeys.InlineCell],
            nameSlot: "text",
            description: "A symbol the notation itself provides."),

        new ConceptDeclaration(
            EditorConceptKeys.Property,
            [new PropertySlot("slot", PropertyType.String)],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "The text of a property."),

        new ConceptDeclaration(
            EditorConceptKeys.Reference,
            [new PropertySlot("slot", PropertyType.String)],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "The name of a reference's target."),

        new ConceptDeclaration(
            EditorConceptKeys.ChildInline,
            [
                new PropertySlot("slot", PropertyType.String),
                new PropertySlot("emptyText", PropertyType.String, "<empty>"),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "A single child, drawn inline."),

        new ConceptDeclaration(
            EditorConceptKeys.Children,
            [
                new PropertySlot("slot", PropertyType.String),
                new PropertySlot("emptyText", PropertyType.String, "<empty>"),
                new PropertySlot("spacing", PropertyType.Integer, "1"),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Many children, drawn as blocks."),

        // Inline, and declared after the block cells on purpose: `abstract` is a word inside the concept's header
        // line, so the only place a conditional can stand today is inside a line.
        new ConceptDeclaration(
            EditorConceptKeys.Conditional,
            [
                new PropertySlot("slot", PropertyType.String),
                new PropertySlot("value", PropertyType.String),
                new ChildSlot(CellSlot, EditorConceptKeys.InlineCell, Cardinality.Many),
            ],
            [EditorConceptKeys.InlineCell],
            nameSlot: "slot",
            description: "Inline cells drawn only while a property holds a value."),

        new ConceptDeclaration(
            EditorConceptKeys.Collection,
            [
                new PropertySlot("slot", PropertyType.String),
                new PropertySlot("emptyText", PropertyType.String, "<empty>"),
                new ChildSlot("separator", EditorConceptKeys.InlineCell, Cardinality.One),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Many children with a separator between them, drawn as blocks."),

        new ConceptDeclaration(
            EditorConceptKeys.Table,
            [
                new PropertySlot("slot", PropertyType.String),
                new PropertySlot("emptyText", PropertyType.String, "<empty>"),
                new ChildSlot(ColumnSlot, EditorConceptKeys.Column, Cardinality.Many),
            ],
            [EditorConceptKeys.BlockCell],
            nameSlot: "slot",
            description: "Many children drawn as a grid: one row per child, one cell per column."),

        new ConceptDeclaration(
            EditorConceptKeys.Column,
            [
                new PropertySlot("slot", PropertyType.String),
                new PropertySlot("emptyText", PropertyType.String, "<empty>"),
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
    public static Model NewModel(string name = "editor") => NewProject().NewModel(name);
}
