using Projectional.Language;

namespace Projectional.Samples.SqlSchema;

/// <summary>The schema as Data Definition Language: one <c>create table</c> per table, then its keys.</summary>
/// <remarks>
/// <para>
/// A fragment rather than a runnable script, which is the same claim the pilot's C# generator makes: a key is
/// emitted as <c>foreign key (&lt;column&gt;) references &lt;column&gt;</c> without the <c>alter table</c> that would
/// hang it on a table, because a template cannot read the name of the node that *holds* the node it is expanding.
/// That is a gap in the part set, recorded in <c>docs/architecture/worked-examples.md</c>, not an oversight of this
/// language.
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
            new IndentPart(TextTemplate.Of(
                LineBreakPart.Instance,
                new ChildrenPart(SqlSchemaLanguage.KeysSlot, TextTemplate.Of(LineBreakPart.Instance))))))

        .Add(SqlConceptKeys.Column, TextTemplate.Of(
            new PropertyPart("name"),
            new TextPart(" "),
            new PropertyPart("type")))

        .Add(SqlConceptKeys.ForeignKey, TextTemplate.Of(
            new TextPart("foreign key ("),
            new ReferencePart(SqlSchemaLanguage.LocalSlot),
            new TextPart(") references "),
            new ReferencePart(SqlSchemaLanguage.TargetSlot))));

    /// <summary>Every generator this language declares, in the order a menu should list them.</summary>
    public static IReadOnlyList<TextGenerator> All { get; } = [Ddl];
}
