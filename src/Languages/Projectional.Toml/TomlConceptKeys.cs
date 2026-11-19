using System.Diagnostics.CodeAnalysis;

namespace Projectional.Toml;

/// <summary>The keys of the TOML language: one concept per thing a TOML document is made of.</summary>
/// <remarks>
/// Fourteen concepts and one container, because unlike JSON a TOML file is not one value: the order of its key/value
/// pairs and its headers *is* its structure, so <see cref="Document"/> is a concept of its own rather than a wrapper
/// somebody invented.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "The members are the format's own words - a table, a string, a datetime - so the constant that spells 'Toml.String' reads as the concept key it is. Renaming them around a type name would make the language's vocabulary less recognizable, not more.")]
public static class TomlConceptKeys
{
    /// <summary>The document: its entries in the order the file has them.</summary>
    public const string Document = "Toml.Document";

    /// <summary>One entry of a document or of a table: a pair, a header, an array of tables, or a note.</summary>
    public const string Entry = "Toml.Entry";

    /// <summary>A pair in the table that is open where it stands.</summary>
    public const string KeyValue = "Toml.KeyValue";

    /// <summary>A header: a table's name and the entries written under it.</summary>
    public const string Table = "Toml.Table";

    /// <summary>A header in double brackets: one element of a list of tables.</summary>
    public const string ArrayOfTables = "Toml.ArrayOfTables";

    /// <summary>Anything a pair can hold.</summary>
    public const string Value = "Toml.Value";

    /// <summary>A table written in one line, as a value rather than as a header.</summary>
    public const string InlineTable = "Toml.InlineTable";

    /// <summary>A string, in one of the four spellings the format has.</summary>
    public const string String = "Toml.String";

    /// <summary>An integer, held as the literal the file had.</summary>
    public const string Integer = "Toml.Integer";

    /// <summary>A float, held as the literal the file had.</summary>
    public const string Float = "Toml.Float";

    /// <summary>A boolean, as an enumeration so the cell offers the two values.</summary>
    public const string Bool = "Toml.Bool";

    /// <summary>A date, a time or both, in one of the four kinds the format has.</summary>
    public const string DateTime = "Toml.DateTime";

    /// <summary>An array of values.</summary>
    public const string Array = "Toml.Array";

    /// <summary>A note, kept where the file had it.</summary>
    public const string Comment = "Toml.Comment";
}
