using Projectional.Language;

namespace Projectional.Editors.Rendering;

/// <summary>
/// How a notation is drawn: one line per notation, its cells nested underneath.
/// </summary>
/// <remarks>
/// Provisional, like the structure language's own notation and for the same reason: the MPS-shaped glyphs for
/// cells are slice P5. What matters here is that a notation is a model that can be opened and read - a definition
/// nobody can look at is a data structure, not a language.
/// </remarks>
public static class EditorNotations
{
    /// <summary>Registers the notation of every concept of the editor language.</summary>
    /// <param name="notations">The registry to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        var ed = NotationEditor.Create();

        notations.Add(Projectional.Editors.EditorConceptKeys.Notations, ed => ed.Children(
            EditorLanguage.NotationSlot,
            "<no notations>"));

        notations.Add(Projectional.Editors.EditorConceptKeys.Notation, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("notation"),
                ed.Space,
                ed.Property(EditorLanguage.ConceptKeySlot),
                ed.Space,
                ed.Keyword("variant"),
                ed.Property("variant")),
            ed.Indent(ed.Sequence(ed.Line(ed.ChildInline(EditorLanguage.RootSlot, "<no root>"))))));

        notations.Add(Projectional.Editors.EditorConceptKeys.Cell, ed => ed.Line(ed.Keyword("cell")));
        notations.Add(Projectional.Editors.EditorConceptKeys.InlineCell, ed => ed.Line(ed.Keyword("inline")));
        notations.Add(Projectional.Editors.EditorConceptKeys.BlockCell, ed => ed.Line(ed.Keyword("block")));
        notations.Add(Projectional.Editors.EditorConceptKeys.Space, ed => ed.Line(ed.Keyword("space")));

        notations.Add(Projectional.Editors.EditorConceptKeys.Line, ed => ed.Sequence(
            ed.Line(ed.Keyword("line")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, "<empty line>"))));

        notations.Add(Projectional.Editors.EditorConceptKeys.Sequence, ed => ed.Sequence(
            ed.Line(ed.Keyword("sequence")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, "<empty sequence>"))));

        notations.Add(Projectional.Editors.EditorConceptKeys.Indent, ed => ed.Sequence(
            ed.Line(ed.Keyword("indent")),
            ed.Indent(ed.Children(EditorLanguage.CellSlot, "<empty indent>"))));

        notations.Add(Projectional.Editors.EditorConceptKeys.Keyword, ed => ed.Line(
            ed.Keyword("keyword"),
            ed.Space,
            ed.Punct("\""),
            ed.Property("text"),
            ed.Punct("\"")));

        notations.Add(Projectional.Editors.EditorConceptKeys.Punct, ed => ed.Line(
            ed.Keyword("punct"),
            ed.Space,
            ed.Punct("\""),
            ed.Property("text"),
            ed.Punct("\"")));

        notations.Add(Projectional.Editors.EditorConceptKeys.Property, ed => ed.Line(
            ed.Keyword("property"),
            ed.Space,
            ed.Property("slot")));

        notations.Add(Projectional.Editors.EditorConceptKeys.Reference, ed => ed.Line(
            ed.Keyword("reference"),
            ed.Space,
            ed.Property("slot")));

        notations.Add(Projectional.Editors.EditorConceptKeys.ChildInline, ed => ed.Line(
            ed.Keyword("child"),
            ed.Space,
            ed.Property("slot"),
            ed.Space,
            ed.Keyword("empty"),
            ed.Property("emptyText")));

        notations.Add(Projectional.Editors.EditorConceptKeys.Children, ed => ed.Line(
            ed.Keyword("children"),
            ed.Space,
            ed.Property("slot"),
            ed.Space,
            ed.Keyword("empty"),
            ed.Property("emptyText"),
            ed.Space,
            ed.Keyword("spacing"),
            ed.Property("spacing")));
    }

    /// <summary>A registry holding only the editor language.</summary>
    public static NotationRegistry Create()
    {
        var notations = new NotationRegistry(EditorLanguage.Concepts);

        AddTo(notations);

        return notations;
    }
}
