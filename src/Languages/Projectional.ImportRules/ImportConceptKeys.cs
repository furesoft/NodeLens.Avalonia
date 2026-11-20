namespace Projectional.ImportRules;

/// <summary>The concept keys text import is declared with.</summary>
public static class ImportConceptKeys
{
    /// <summary>One importer: the generator it inverts and the rule per concept.</summary>
    public const string Importer = "Import.Importer";

    /// <summary>A part of a rule, of which there are four.</summary>
    public const string Part = "Import.Part";

    /// <summary>What one concept looks like in the text.</summary>
    public const string Rule = "Import.Rule";

    /// <summary>Text that has to stand at this point of the rule.</summary>
    public const string Literal = "Import.Literal";

    /// <summary>The run up to the next literal, filled into a property.</summary>
    public const string Capture = "Import.Capture";

    /// <summary>The child in a slot, read by whichever rule the slot allows.</summary>
    public const string Nest = "Import.Nest";

    /// <summary>The children of a list, separated by a literal.</summary>
    public const string Repeat = "Import.Repeat";

    /// <summary>A named token class, of which there are four.</summary>
    public const string Token = "Import.Token";

    /// <summary>A run of letters, digits and the characters the declaration adds.</summary>
    public const string Word = "Import.Word";

    /// <summary>A number, with an optional sign, fraction and exponent.</summary>
    public const string Number = "Import.Number";

    /// <summary>A run between two marks, with the escapes the declaration names resolved.</summary>
    public const string Quoted = "Import.Quoted";

    /// <summary>Text that produces no node: whitespace, or a comment.</summary>
    public const string Skipped = "Import.Skipped";
}
