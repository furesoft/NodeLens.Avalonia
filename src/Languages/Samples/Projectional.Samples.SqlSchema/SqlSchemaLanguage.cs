using Projectional.Core;
using Projectional.Language;

namespace Projectional.Samples.SqlSchema;

/// <summary>
/// A relational schema: tables of columns, and foreign keys that name a column of another table.
/// </summary>
/// <remarks>
/// <para>
/// The third of the three worked examples (docs/architecture/worked-examples.md). Its axes are the two the
/// other two leave alone: a reference that leaves its **own model** - a project holds several documents, and
/// the default scope chain walks up to the project level, so a key in `orders.mps` can point at a column in
/// `shop.mps` without either document importing the other - and the **table cell** drawing a schema rather
/// than a rule table.
/// </para>
/// <para>
/// A key is a child of the table that declares it rather than a property of a column, for one reason that is
/// about text rather than about modelling: a column written as `<name> <type>` cannot say "and a reference"
/// only when it has one, because no template part asks whether a slot is filled. A key as its own node is
/// emitted when it exists and not otherwise, which is the same reason the feature model states its rules as
/// nodes.
/// </para>
/// </remarks>
public static class SqlSchemaLanguage
{
    /// <summary>The slot of a schema that holds its tables.</summary>
    public const string TablesSlot = "tables";

    /// <summary>The slot of a table that holds its columns.</summary>
    public const string ColumnsSlot = "columns";

    /// <summary>The slot of a table that holds its keys.</summary>
    public const string KeysSlot = "keys";

    /// <summary>The reference a key uses to name the column it constrains.</summary>
    public const string LocalSlot = "local";

    /// <summary>The reference a key uses to name the column that holds the value.</summary>
    public const string TargetSlot = "target";

    /// <summary>The column types this language offers, in the order a completion list shows them.</summary>
    public static PropertyType ColumnType { get; } = PropertyType.Enum(
        "int",
        "text",
        "bool",
        "date",
        "decimal");

    /// <summary>The concepts this language adds, which are the only ones it declares.</summary>
    public static IReadOnlyList<ConceptDeclaration> ConceptDeclarations { get; } = CreateDeclarations();

    /// <summary>The registry this language declares.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(ConceptDeclarations);

    /// <summary>A registry for the concepts of this language.</summary>
    /// <param name="concepts">The registry to draw with.</param>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);
        SqlSchemaNotations.AddTo(notations);

        return notations;
    }

    /// <summary>A project for a document of this language.</summary>
    /// <remarks>
    /// A project, not a model: a schema is several documents - one per subsystem, which is what makes the
    /// cross-model reference the point of this example.
    /// </remarks>
    public static Project NewProject() => new(Concepts);

    private static List<ConceptDeclaration> CreateDeclarations() =>
    [
        new ConceptDeclaration(
            SqlConceptKeys.Schema,
            [
                new PropertySlot("name", PropertyType.String, "schema"),
                new ChildSlot(TablesSlot, SqlConceptKeys.Table, Cardinality.Many),
            ],
            description: "One document of a schema: the tables it holds.",
            interfaces: [FrameworkInterfaces.Named],
            nameRule: NameRule.Snake),

        new ConceptDeclaration(
            SqlConceptKeys.Table,
            [
                new PropertySlot("name", PropertyType.String, "table"),
                new ChildSlot(ColumnsSlot, SqlConceptKeys.Column, Cardinality.Many),
                new ChildSlot(KeysSlot, SqlConceptKeys.ForeignKey, Cardinality.Many),
            ],
            description: "A table: its columns, and the keys that point at other rows.",
            interfaces: [FrameworkInterfaces.Named],
            nameRule: NameRule.Snake),

        new ConceptDeclaration(
            SqlConceptKeys.Column,
            [
                new PropertySlot("name", PropertyType.String, "column"),
                new PropertySlot(
                    "type",
                    ColumnType,
                    "int",
                    "What values the column holds.",
                    showDescriptionWhenFilled: true),
            ],
            description: "One column of a table.",
            interfaces: [FrameworkInterfaces.Named],
            nameRule: NameRule.Snake),

        new ConceptDeclaration(
            SqlConceptKeys.ForeignKey,
            [
                // `local` names a column of the table that holds this key, so it is a reference into the
                // referring node's own subtree - the case the model refuses unless the slot says it may.
                new ReferenceSlot(LocalSlot, SqlConceptKeys.Column, allowOwnSubtree: true),

                // `target` is deliberately *not* lifted: a key may name any column of the project, and the
                // interesting one is in another document, which no own-subtree rule is about.
                new ReferenceSlot(TargetSlot, SqlConceptKeys.Column),
            ],
            description: "A key: this table's column holds a value that identifies a row of another table.",
            interfaces: [FrameworkInterfaces.Named]),
    ];
}
