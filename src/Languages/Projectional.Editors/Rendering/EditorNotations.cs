using Projectional.Language;

namespace Projectional.Editors.Rendering;

/// <summary>
/// How a notation is drawn, in the shape MPS writes an editor model: which concept, which variant, and the cell
/// layout underneath.
/// </summary>
/// <remarks>
/// <para>
/// The brackets are the point. MPS draws a vertical collection as <c>[/ ... /]</c> and an inline cell as
/// <c>[&gt;</c>, so a reader can see the shape of the tree without running it - a <c>[/</c> opens a block that
/// stacks, and every line inside it is one row. Copying that gives the same reading in a projectional editor:
/// the structure of the notation is the structure of the drawing.
/// </para>
/// <para>
/// One difference is deliberate and worth knowing: in MPS the cells of a line sit on the line, while here an inline
/// cell is shown as its own <c>[&gt;</c> row, because a line's notation cannot hold a child block. The shape of the
/// <em>model</em> is unchanged - it is still a line of inline cells - and the notation of the notation has to make
/// the same compromise any notation does when it draws a language with deeper structure than its own.
/// </para>
/// </remarks>
public static class EditorNotations
{
    private const string NoEntries = "<< ... >>";

    /// <summary>Registers the notation of every concept of the editor language.</summary>
    /// <param name="notations">The registry to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        var ed = NotationEditor.Create();

        notations.Add(EditorConceptKeys.Notation, ed => ed.Indent(ed.Sequence(
            ed.Line(
                ed.Keyword("editor for concept"),
                ed.Space,
                ed.Property(EditorLanguage.ConceptKeySlot),
                ed.Space,
                ed.Keyword("variant:"),
                ed.Space,
                ed.Property("variant")),
            ed.Indent(ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("node cell layout:")),
                ed.Line(ed.ChildInline(EditorLanguage.RootSlot, "<no layout>"))))))));

        notations.Add(EditorConceptKeys.Cell, ed => ed.Line(ed.Keyword("cell")));
        notations.Add(EditorConceptKeys.InlineCell, ed => ed.Line(ed.Keyword("inline cell")));
        notations.Add(EditorConceptKeys.BlockCell, ed => ed.Line(ed.Keyword("block cell")));
        notations.Add(EditorConceptKeys.Space, ed => ed.Line(ed.Punct(";")));

        // The one cell that draws what the model means rather than what the notation says, so it gets a marker of its
        // own: `@type@` reads as "this is computed" in a line that otherwise only spells out literal text.
        notations.Add(EditorConceptKeys.TypeOf, ed => ed.Line(ed.Annotation("@type@")));

        // `[ cells ]`: the brackets beside the cells, on one row - which is what the horizontal cell is for. Without
        // it the bracket would have to be a line of its own above the cells.
        notations.Add(EditorConceptKeys.Line, ed => ed.Horizontal(
            ed.Line(ed.Punct("[")),
            ed.Children(EditorLanguage.CellSlot, NoEntries),
            ed.Line(ed.Punct("]"))));

        notations.Add(EditorConceptKeys.Sequence, ed => ed.Sequence(
            ed.Line(ed.Keyword("[/")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries, stacked: true)),
            ed.Line(ed.Keyword("/]"))));

        notations.Add(EditorConceptKeys.Indent, ed => ed.Sequence(
            ed.Line(ed.Keyword("[>")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries, stacked: true)),
            ed.Line(ed.Keyword("<]"))));

        // The one block container whose children are not below one another, so it is bracketed differently - the
        // brackets are what a reviewer reads the direction off.
        notations.Add(EditorConceptKeys.Horizontal, ed => ed.Sequence(
            ed.Line(ed.Keyword("[|")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries, stacked: true)),
            ed.Line(ed.Keyword("|]"))));

        notations.Add(EditorConceptKeys.Keyword, ed => ed.Line(
            ed.Punct("-"),
            ed.Property("text"),
            ed.Punct("-")));

        notations.Add(EditorConceptKeys.Punct, ed => ed.Line(
            ed.Punct("\""),
            ed.Property("text"),
            ed.Punct("\"")));

        notations.Add(EditorConceptKeys.Property, ed => ed.Line(
            ed.Punct("%"),
            ed.Property("slot"),
            ed.Punct("%"),
            ed.Space,
            ed.Keyword("empty:"),
            ed.Space,
            ed.Property("emptyText")));

        notations.Add(EditorConceptKeys.Reference, ed => ed.Line(
            ed.Punct("<"),
            ed.Property("slot"),
            ed.Punct(">")));

        notations.Add(EditorConceptKeys.ChildInline, ed => ed.Line(
            ed.Punct("("),
            ed.Property("slot"),
            ed.Punct(")"),
            ed.Space,
            ed.Keyword("empty:"),
            ed.Space,
            ed.Property("emptyText")));

        notations.Add(EditorConceptKeys.Children, ed => ed.Line(
            ed.Punct("{"),
            ed.Property("slot"),
            ed.Punct("}"),
            ed.Space,
            ed.Keyword("empty:"),
            ed.Space,
            ed.Property("emptyText"),
            ed.Space,
            ed.Keyword("spacing:"),
            ed.Space,
            ed.Property("spacing"),
            ed.Space,
            ed.Keyword("stacked:"),
            ed.Space,
            ed.Property("stacked")));

        // `[> if {slot} = "value"` reads as the question the cell asks, and the cells below it are what is drawn
        // while the answer is yes.
        notations.Add(EditorConceptKeys.Conditional, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[> if"),
                ed.Space,
                ed.Property("slot"),
                ed.Space,
                ed.Punct("="),
                ed.Space,
                ed.Punct("\""),
                ed.Property("value"),
                ed.Punct("\"")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries))));

        // The sibling of the conditional: the same question, asked of a slot instead of a property. It reads as the
        // condition an author has to satisfy, because the cells below it only exist while the slot is filled.
        notations.Add(EditorConceptKeys.WhileFilled, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[> while"),
                ed.Space,
                ed.Property("slot"),
                ed.Space,
                ed.Keyword("is filled")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries))));

        // The block form of the property conditional, bracketed like a block for the same reason: the group is absent
        // from the drawing rather than drawn as an empty row.
        notations.Add(EditorConceptKeys.ConditionalBlock, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[/ if"),
                ed.Space,
                ed.Property("slot"),
                ed.Space,
                ed.Punct("="),
                ed.Space,
                ed.Punct("\""),
                ed.Property("value"),
                ed.Punct("\"")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries, stacked: true)),
            ed.Line(ed.Keyword("/]"))));
        // The block sibling, bracketed like a block: `[/` opens it, and the whole group is absent from the drawing
        // while the slot is empty rather than drawn as an empty row.
        notations.Add(EditorConceptKeys.WhileFilledBlock, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[/ while"),
                ed.Space,
                ed.Property("slot"),
                ed.Space,
                ed.Keyword("is filled")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries, stacked: true)),
            ed.Line(ed.Keyword("/]"))));

        notations.Add(EditorConceptKeys.Collection, ed => ed.Line(
            ed.Punct("{"),
            ed.Property("slot"),
            ed.Punct("}"),
            ed.Space,
            ed.Keyword("empty:"),
            ed.Space,
            ed.Property("emptyText"),
            ed.Space,
            ed.Keyword("separator:"),
            ed.Space,
            ed.ChildInline("separator")));

        // `[> {slot}` opens the inline form, the bracket a reader already knows from every other inline cell.
        notations.Add(EditorConceptKeys.InlineCollection, ed => ed.Line(
            ed.Punct("[>"),
            ed.Punct("{"),
            ed.Property("slot"),
            ed.Punct("}"),
            ed.Space,
            ed.Keyword("empty:"),
            ed.Space,
            ed.Property("emptyText"),
            ed.Space,
            ed.Keyword("spacing:"),
            ed.Space,
            ed.Property("spacing"),
            ed.Space,
            ed.Keyword("separator:"),
            ed.Space,
            ed.ChildInline("separator")));

        notations.Add(EditorConceptKeys.Table, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[/ table"),
                ed.Space,
                ed.Property("slot"),
                ed.Space,
                ed.Keyword("empty:"),
                ed.Space,
                ed.Property("emptyText")),
            ed.Indent(ed.Children(EditorLanguage.ColumnSlot, NoEntries, stacked: true)),
            ed.Line(ed.Keyword("/]"))));

        notations.Add(EditorConceptKeys.Column, ed => ed.Line(
            ed.Keyword("column"),
            ed.Space,
            ed.Property("slot"),
            ed.Space,
            ed.Keyword("as"),
            ed.Space,
            ed.ChildInline("header"),
            ed.Space,
            ed.Keyword("empty:"),
            ed.Space,
            ed.Property("emptyText")));
    }

    /// <summary>A registry holding only the editor language.</summary>
    public static NotationRegistry Create()
    {
        var notations = new NotationRegistry(EditorLanguage.Concepts);

        AddTo(notations);

        return notations;
    }
}
