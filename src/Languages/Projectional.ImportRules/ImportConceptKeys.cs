namespace Projectional.ImportRules;

/// <summary>The concept keys text import is declared with.</summary>
public static class ImportConceptKeys
{
    /// <summary>One importer: the generator it inverts and the rule per concept.</summary>
    public const string Importer = "Import.Importer";

    /// <summary>A part of a rule, of which there are nine.</summary>
    public const string Part = "Import.Part";

    /// <summary>What one concept looks like in the text.</summary>
    public const string Rule = "Import.Rule";

    /// <summary>Text that has to stand at this point of the rule.</summary>
    public const string Literal = "Import.Literal";

    /// <summary>The run up to the next literal, filled into a property.</summary>
    public const string Capture = "Import.Capture";

    /// <summary>The rest of the line, filled into a property.</summary>
    public const string Line = "Import.Line";

    /// <summary>The child in a slot, read by whichever rule the slot allows.</summary>
    public const string Nest = "Import.Nest";

    /// <summary>The children of a list, separated by a literal.</summary>
    public const string Repeat = "Import.Repeat";

    /// <summary>A named token class, of which there are five.</summary>
    public const string Token = "Import.Token";

    /// <summary>A run of letters, digits and the characters the declaration adds.</summary>
    public const string Word = "Import.Word";

    /// <summary>A number, with an optional sign, fraction and exponent.</summary>
    public const string Number = "Import.Number";

    /// <summary>A run between two marks, with the escapes the declaration names resolved.</summary>
    public const string Quoted = "Import.Quoted";

    /// <summary>Text that produces no node: whitespace, or a comment.</summary>
    public const string Skipped = "Import.Skipped";

    /// <summary>A run between two marks in which nothing is escaped.</summary>
    public const string Fixed = "Import.Fixed";

    /// <summary>A run the model does not map, kept verbatim.</summary>
    public const string Raw = "Import.Raw";

    /// <summary>A run remembered under a name, filling no slot.</summary>
    public const string Bind = "Import.Bind";

    /// <summary>The next run required to be the one a bind remembered.</summary>
    public const string Matches = "Import.Matches";

    /// <summary>The message a rule gives instead of "expected".</summary>
    public const string Fail = "Import.Fail";
}
