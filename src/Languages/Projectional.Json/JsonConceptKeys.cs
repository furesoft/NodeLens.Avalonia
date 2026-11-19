using System.Diagnostics.CodeAnalysis;

namespace Projectional.Json;

/// <summary>The keys of the JSON language: one concept per thing a JSON document can hold.</summary>
/// <remarks>
/// Eight concepts and no container, because a JSON document *is* one value: the root of a document is whatever the file
/// had at its top level, and a <c>Json.Document</c> wrapper would be a second place to say what the file is. That is the
/// rule the definition languages follow as well - one root per document.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "The members are the format's own words - an object, an array, a string - so the constant that spells 'Json.Object' reads as the concept key it is. Renaming them around a type name would make the language's vocabulary less recognizable, not more.")]
public static class JsonConceptKeys
{
    /// <summary>Any value: the abstract super concept every other one here conforms to.</summary>
    public const string Value = "Json.Value";

    /// <summary>One entry of an object: a member, or a note beside one.</summary>
    public const string Member = "Json.Member";

    /// <summary>A note the format cannot express, kept in the model and left out of the generated text.</summary>
    public const string Comment = "Json.Comment";

    /// <summary>A set of members.</summary>
    public const string Object = "Json.Object";

    /// <summary>One member: a name and the value it holds.</summary>
    public const string Property = "Json.Property";

    /// <summary>An ordered list of values.</summary>
    public const string Array = "Json.Array";

    /// <summary>A string, held as the file spells it.</summary>
    public const string Text = "Json.String";

    /// <summary>A number, held as the literal the file had.</summary>
    public const string Number = "Json.Number";

    /// <summary>A boolean, as an enumeration so the cell offers the two values.</summary>
    public const string Bool = "Json.Bool";

    /// <summary>The null value; a concept rather than an absent slot, because the file said it.</summary>
    public const string Null = "Json.Null";
}
