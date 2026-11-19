namespace Projectional.Samples.SqlSchema;

/// <summary>The keys of the concepts <see cref="SqlSchemaLanguage"/> declares.</summary>
public static class SqlConceptKeys
{
    /// <summary>One document of a schema: the tables it holds.</summary>
    public const string Schema = "Sql.Schema";

    /// <summary>One table: its columns and the keys it declares.</summary>
    public const string Table = "Sql.Table";

    /// <summary>One column of a table.</summary>
    public const string Column = "Sql.Column";

    /// <summary>One key: a column of this table naming a column of some table.</summary>
    public const string ForeignKey = "Sql.ForeignKey";
}
