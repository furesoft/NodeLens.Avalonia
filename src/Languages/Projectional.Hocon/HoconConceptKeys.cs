using System.Diagnostics.CodeAnalysis;

namespace Projectional.Hocon;

/// <summary>The keys of the HOCON language: one concept per thing a HOCON file is made of.</summary>
/// <remarks>
/// HOCON is JSON with the punctuation made optional, so the vocabulary is JSON's plus the four constructs that make it
/// HOCON: a property whose separator is one of four spellings, juxtaposition as concatenation, a substitution and an
/// include. Both unions exist because a comment and an include stand where a property stands but are not values, which
/// is the same split <c>Json.Member</c> makes.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "The members are the format's own words - an object, a string, a number - so the constant that spells 'Hocon.String' reads as the concept key it is. Renaming them around a type name would make the language's vocabulary less recognizable, not more.")]
public static class HoconConceptKeys
{
    /// <summary>The file: a brace-less object whose members are what it holds.</summary>
    public const string Document = "Hocon.Document";

    /// <summary>One entry of a document or of an object: a property, an include, or a note.</summary>
    public const string Member = "Hocon.Member";

    /// <summary>Anything a property can hold.</summary>
    public const string Value = "Hocon.Value";

    /// <summary>An object written between braces.</summary>
    public const string Object = "Hocon.Object";

    /// <summary>A key, the separator it was written with, and its value.</summary>
    public const string Property = "Hocon.Property";

    /// <summary>An include, kept as the text it was written as and never followed.</summary>
    public const string Include = "Hocon.Include";

    /// <summary>A note, with the marker that introduced it.</summary>
    public const string Comment = "Hocon.Comment";

    /// <summary>An array of values.</summary>
    public const string Array = "Hocon.Array";

    /// <summary>A string, in one of the three spellings the format has.</summary>
    public const string String = "Hocon.String";

    /// <summary>A number, held as the literal the file had.</summary>
    public const string Number = "Hocon.Number";

    /// <summary>A boolean, as an enumeration so the cell offers the two values.</summary>
    public const string Bool = "Hocon.Bool";

    /// <summary>The null value, as a concept rather than an absent slot.</summary>
    public const string Null = "Hocon.Null";

    /// <summary>Values written next to each other, which HOCON reads as one concatenated value.</summary>
    public const string Concat = "Hocon.Concat";

    /// <summary>A substitution: a name that is resolved when the configuration is read, never here.</summary>
    public const string Substitution = "Hocon.Substitution";
}
