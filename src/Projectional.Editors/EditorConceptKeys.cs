namespace Projectional.Editors;

/// <summary>The keys of the editor language: the concepts a notation is written with.</summary>
/// <remarks>
/// Nineteen concepts, and the split between them is the one rule the C# builder expresses with its type hierarchy:
/// a <see cref="Line"/> runs horizontally and holds only inline cells, while a <see cref="Sequence"/> and an
/// <see cref="Indent"/> stack and hold only blocks. A declared notation therefore cannot express what
/// <c>ed.Line(ed.Indent(...))</c> cannot compile - the rule moved from the compiler into the slot declarations.
/// </remarks>
public static class EditorConceptKeys
{
    /// <summary>The document: one notation per concept.</summary>
    public const string Notations = "Editor.Notations";

    /// <summary>How one concept is drawn, in one variant.</summary>
    public const string Notation = "Editor.Notation";

    /// <summary>Anything that can be drawn, abstract because every cell is inline or a block.</summary>
    public const string Cell = "Editor.Cell";

    /// <summary>A cell that runs inside a line.</summary>
    public const string InlineCell = "Editor.InlineCell";

    /// <summary>A cell that occupies whole lines.</summary>
    public const string BlockCell = "Editor.BlockCell";

    /// <summary>One horizontal line of inline cells.</summary>
    public const string Line = "Editor.Line";

    /// <summary>Blocks stacked without indentation.</summary>
    public const string Sequence = "Editor.Sequence";

    /// <summary>Blocks stacked one level deeper.</summary>
    public const string Indent = "Editor.Indent";

    /// <summary>A gap.</summary>
    public const string Space = "Editor.Space";

    /// <summary>A word the notation itself provides.</summary>
    public const string Keyword = "Editor.Keyword";

    /// <summary>A symbol the notation itself provides.</summary>
    public const string Punct = "Editor.Punct";

    /// <summary>The text of a property.</summary>
    public const string Property = "Editor.Property";

    /// <summary>The name of a reference's target.</summary>
    public const string Reference = "Editor.Reference";

    /// <summary>A single child, inline.</summary>
    public const string ChildInline = "Editor.ChildInline";

    /// <summary>Many children, as blocks.</summary>
    public const string Children = "Editor.Children";

    /// <summary>Many children with a separator between them, as blocks.</summary>
    public const string Collection = "Editor.Collection";

    /// <summary>Inline cells drawn only while a property of the node holds a value.</summary>
    public const string Conditional = "Editor.Conditional";

    /// <summary>Inline cells drawn only while a child slot of the node holds a child.</summary>
    public const string WhileFilled = "Editor.WhileFilled";

    /// <summary>Many children drawn as a grid: one row per child, one cell per column.</summary>
    public const string Table = "Editor.Table";

    /// <summary>One column of a table: a slot of the row concept, under a header.</summary>
    public const string Column = "Editor.Column";
}
