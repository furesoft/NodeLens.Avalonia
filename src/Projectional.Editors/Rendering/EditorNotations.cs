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

        notations.Add(EditorConceptKeys.Notations, ed => ed.Sequence(
            ed.Line(ed.Keyword("notations:")),
            ed.Indent(ed.Children(EditorLanguage.NotationSlot, NoEntries))));

        notations.Add(EditorConceptKeys.Notation, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("editor for concept"),
                ed.Space,
                ed.Property(EditorLanguage.ConceptKeySlot),
                ed.Space,
                ed.Keyword("variant:"),
                ed.Space,
                ed.Property("variant")),
            ed.Line(ed.Keyword("node cell layout:")),
            ed.Indent(ed.Sequence(ed.Line(ed.ChildInline(EditorLanguage.RootSlot, "<no layout>"))))));

        notations.Add(EditorConceptKeys.Cell, ed => ed.Line(ed.Keyword("cell")));
        notations.Add(EditorConceptKeys.InlineCell, ed => ed.Line(ed.Keyword("inline cell")));
        notations.Add(EditorConceptKeys.BlockCell, ed => ed.Line(ed.Keyword("block cell")));
        notations.Add(EditorConceptKeys.Space, ed => ed.Line(ed.Keyword("[> space")));

        notations.Add(EditorConceptKeys.Line, ed => ed.Sequence(
            ed.Line(ed.Keyword("[> line")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries))));

        notations.Add(EditorConceptKeys.Sequence, ed => ed.Sequence(
            ed.Line(ed.Keyword("[/")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries)),
            ed.Line(ed.Keyword("/]"))));

        notations.Add(EditorConceptKeys.Indent, ed => ed.Sequence(
            ed.Line(ed.Keyword("[/ indent")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, NoEntries)),
            ed.Line(ed.Keyword("/]"))));

        notations.Add(EditorConceptKeys.Keyword, ed => ed.Line(
            ed.Keyword("[>"),
            ed.Space,
            ed.Keyword("constant"),
            ed.Space,
            ed.Punct("\""),
            ed.Property("text"),
            ed.Punct("\"")));

        notations.Add(EditorConceptKeys.Punct, ed => ed.Line(
            ed.Keyword("[>"),
            ed.Space,
            ed.Keyword("punctuation"),
            ed.Space,
            ed.Punct("\""),
            ed.Property("text"),
            ed.Punct("\"")));

        notations.Add(EditorConceptKeys.Property, ed => ed.Line(
            ed.Keyword("[> {"),
            ed.Property("slot"),
            ed.Punct("}")));

        notations.Add(EditorConceptKeys.Reference, ed => ed.Line(
            ed.Keyword("[> reference {"),
            ed.Property("slot"),
            ed.Punct("}")));

        notations.Add(EditorConceptKeys.ChildInline, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[> child <"),
                ed.Space,
                ed.Keyword("empty:"),
                ed.Space,
                ed.Property("emptyText")),
            ed.Indent(ed.Sequence(ed.Line(ed.Property("slot"))))));

        notations.Add(EditorConceptKeys.Children, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[/ children"),
                ed.Space,
                ed.Keyword("empty:"),
                ed.Space,
                ed.Property("emptyText"),
                ed.Space,
                ed.Keyword("spacing:"),
                ed.Space,
                ed.Property("spacing")),
            ed.Indent(ed.Sequence(ed.Line(ed.Property("slot")))),
            ed.Line(ed.Keyword("/]"))));

        notations.Add(EditorConceptKeys.Collection, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("[/ collection"),
                ed.Space,
                ed.Keyword("empty:"),
                ed.Space,
                ed.Property("emptyText")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Property("slot")),
                ed.Line(ed.Keyword("separator:"), ed.Space, ed.ChildInline("separator")))),
            ed.Line(ed.Keyword("/]"))));
    }

    /// <summary>A registry holding only the editor language.</summary>
    public static NotationRegistry Create()
    {
        var notations = new NotationRegistry(EditorLanguage.Concepts);

        AddTo(notations);

        return notations;
    }
}
