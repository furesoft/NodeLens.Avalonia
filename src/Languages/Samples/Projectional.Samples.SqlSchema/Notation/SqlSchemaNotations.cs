using Projectional.Language;

namespace Projectional.Samples.SqlSchema;

/// <summary>How a schema looks: a table per block, its columns as a grid, its keys as lines under it.</summary>
/// <remarks>
/// <para>
/// The table cell is the point of this language: a table's columns are a grid of `name` and `type`, which is
/// what makes a schema readable at a glance rather than a list of declarations. The keys follow as lines,
/// because a key is not a column - it is a statement about one.
/// </para>
/// <para>
/// `references` is a reference cell inside a table column's neighbourhood rather than inside the grid: a
/// <see cref="TableColumn"/> names a slot of the *row* concept, and a key is not a column, so the key's line
/// names both of its ends explicitly.
/// </para>
/// </remarks>
internal static class SqlSchemaNotations
{
    internal static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(SqlConceptKeys.Schema, ed => ed.Sequence(
            ed.Line(ed.Keyword("schema", TextEmphasis.Bold), ed.Space, ed.Property("name"), ed.Space, ed.Punct("{")),
            ed.Indent(ed.Children(SqlSchemaLanguage.TablesSlot, "<none>", stacked: true)),
            ed.Line(ed.Punct("}"))));

        // The grid first, then the keys: a reader looks up the columns, then asks what points where.
        notations.Add(SqlConceptKeys.Table, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("table", TextEmphasis.Bold),
                ed.Space,
                ed.Property("name"),
                ed.Space,
                ed.Punct("{")),
            ed.Indent(ed.Sequence(
                ed.Table(
                    SqlSchemaLanguage.ColumnsSlot,
                    "+ column",
                    ed.Column("name", ed.Keyword("column")),
                    ed.Column("type", ed.Keyword("type"))),

                // `<< ... >>` is the empty-list placeholder the definition languages use: a table with no keys is
                // the ordinary case, and the shared glyph says "nothing here" where `<none>` reads like missing data.
                ed.Children(SqlSchemaLanguage.KeysSlot, "<< ... >>", stacked: true))),
            ed.Line(ed.Punct("}"))));

        notations.Add(SqlConceptKeys.Column, ed => ed.Line(ed.Property("name")));

        // The key is drawn, not typed: an icon says "this line is about a key" in one glance, which is the job a
        // glyph like a padlock does badly in a font that may not have one. The name is a key and not a path - the
        // shell's image palette is what draws "key" (see ImagePalette.KeyKey), and a host that does not know it
        // shows a placeholder box instead of leaving a gap.
        notations.Add(SqlConceptKeys.ForeignKey, ed => ed.Line(
            ed.Image("key"),
            ed.Space,
            ed.Keyword("foreign", TextEmphasis.Italic),
            ed.Space,
            ed.Keyword("key", TextEmphasis.Italic),
            ed.Space,
            ed.Reference(SqlSchemaLanguage.LocalSlot),
            ed.Space,
            ed.Keyword("references"),
            ed.Space,
            ed.Reference(SqlSchemaLanguage.TargetSlot)));
    }
}
