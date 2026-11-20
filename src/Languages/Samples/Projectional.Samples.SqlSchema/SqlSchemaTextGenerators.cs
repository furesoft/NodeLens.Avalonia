using Projectional.Language;

namespace Projectional.Samples.SqlSchema;

/// <summary>The schema as Data Definition Language: one <c>create table</c> per table, then its keys.</summary>
/// <remarks>
/// <para>
/// A fragment rather than a runnable script, which is the same claim the pilot's C# generator makes: the keys are
/// emitted as <c>alter table &lt;table&gt; add foreign key (&lt;column&gt;) references &lt;column&gt;</c>, hanging on
/// the table their template reads with <see cref="ParentPart"/> - the part that arrived when this language could
/// create the tables but not name the one a key belonged to.
/// </para>
/// <para>
/// The output is dialect-free on purpose: `int`, `text` and `bool` are this language's own type names, and a real
/// target would need a mapping the framework has no opinion about.
/// </para>
/// </remarks>
public static class SqlSchemaTextGenerators
{
    /// <summary>The name of the DDL generator.</summary>
    public const string DdlName = "DDL";

    /// <summary>A table's keys, each on a line of its own, indented under the table.</summary>
    /// <remarks>
    /// A template of its own because it is emitted behind a <see cref="WhenFilledPart"/>: the line break in front of
    /// it belongs to the block, so a table that declares no key ends at its closing bracket and does not leave the
    /// indentation of a line that is not there.
    /// </remarks>
    private static readonly TextTemplate KeysBlock = TextTemplate.Of(new IndentPart(TextTemplate.Of(
        LineBreakPart.Instance,
        new ChildrenPart(SqlSchemaLanguage.KeysSlot, TextTemplate.Of(LineBreakPart.Instance)))));

    /// <summary>Every table of a schema as <c>create table</c>, followed by its keys.</summary>
    public static TextGenerator Ddl { get; } = new(DdlName, TemplateRegistry.Empty
        .Add(SqlConceptKeys.Schema, TextTemplate.Of(
            new ChildrenPart(SqlSchemaLanguage.TablesSlot, TextTemplate.Of(LineBreakPart.Instance))))

        .Add(SqlConceptKeys.Table, TextTemplate.Of(
            new TextPart("create table "),
            new PropertyPart("name"),
            new TextPart(" ("),
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                new ChildrenPart(SqlSchemaLanguage.ColumnsSlot, TextTemplate.Of(new TextPart(","), LineBreakPart.Instance)))),
            LineBreakPart.Instance,
            new TextPart(");"),

            // The keys follow the table that declares them. Every table's own creates come first, which is what a
            // key needs; the order *between* tables stays the document's, which a single template walk cannot change.
            // A table without keys emits nothing here, so the line break that separates them is written only when
            // there is something behind it - a trailing indent on a keyless table would be the template's mistake,
            // not a fact about the schema.
            new WhenFilledPart(SqlSchemaLanguage.KeysSlot, KeysBlock)))

        .Add(SqlConceptKeys.Column, TextTemplate.Of(
            new PropertyPart("name"),
            new TextPart(" "),
            new PropertyPart("type")))

        .Add(SqlConceptKeys.ForeignKey, TextTemplate.Of(
            new TextPart("alter table "),
            new ParentPart("name"),
            new TextPart(" add foreign key ("),
            new ReferencePart(SqlSchemaLanguage.LocalSlot),
            new TextPart(") references "),
            new ReferencePart(SqlSchemaLanguage.TargetSlot))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Ddl];
}
