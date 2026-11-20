namespace Projectional.ImportRules;

/// <summary>One part of a lowered rule.</summary>
/// <param name="Kind">Which of the four it is.</param>
/// <param name="Text">The text of a literal, or empty for the other kinds.</param>
/// <param name="Slot">The property or slot the part fills or reads, or empty for a literal.</param>
/// <param name="Separator">What stands between two entries of a repeat, or empty for none.</param>
/// <param name="Minimum">How many entries a repeat needs at least.</param>
/// <remarks>
/// The four kinds share one record because they share one shape once lowered: a rule is a list of parts and the
/// importer asks the same two questions of each one - how much text can this part claim, and what does it fill. The
/// author never sees this type; the language's four concepts are what a rule is written with.
/// </remarks>
public sealed record ImportPart(
    ImportPartKind Kind,
    string Text,
    string Slot,
    string Separator,
    int Minimum,
    string Name = "",
    string Message = "")
{
    /// <summary>A part that has to find <paramref name="text"/>.</summary>
    /// <param name="text">The text to find.</param>
    public static ImportPart Literal(string text) =>
        new(ImportPartKind.Literal, text, string.Empty, string.Empty, 0);

    /// <summary>A part that fills <paramref name="slot"/> with one token, or the run up to the next literal.</summary>
    /// <param name="slot">The property to fill.</param>
    /// <param name="token">The token class to read, or empty to run to the next literal.</param>
    public static ImportPart Capture(string slot, string token = "") =>
        new(ImportPartKind.Capture, token, slot, string.Empty, 0);

    /// <summary>A part that reads one child of <paramref name="slot"/>.</summary>
    /// <param name="slot">The child slot to fill.</param>
    public static ImportPart Nest(string slot) =>
        new(ImportPartKind.Nest, string.Empty, slot, string.Empty, 0);

    /// <summary>A part that keeps the run up to <paramref name="until"/> in <paramref name="slot"/>, exactly as written.</summary>
    /// <param name="slot">The property the run is kept in.</param>
    /// <param name="until">The text that ends the run.</param>
    public static ImportPart Raw(string slot, string until) =>
        new(ImportPartKind.Raw, until, slot, string.Empty, 0);

    /// <summary>A part that remembers the next run under <paramref name="name"/> and fills no slot.</summary>
    /// <param name="name">The name the run is remembered under.</param>
    /// <param name="token">The token class to read, or empty to run to the next literal.</param>
    public static ImportPart Bind(string name, string token) =>
        new(ImportPartKind.Bind, token, string.Empty, string.Empty, 0, name);

    /// <summary>A part that requires the next run to be the one <paramref name="name"/> remembered.</summary>
    /// <param name="name">The name a bind earlier in the same rule remembered.</param>
    /// <param name="token">The token class to read, or empty to run to the next literal.</param>
    public static ImportPart Matches(string name, string token) =>
        new(ImportPartKind.Matches, token, string.Empty, string.Empty, 0, name);

    /// <summary>A part that refuses with the rule's own message.</summary>
    /// <param name="code">The identifier the refusal reports under.</param>
    /// <param name="message">The sentence a reader gets.</param>
    public static ImportPart Fail(string code, string message) =>
        new(ImportPartKind.Fail, code, string.Empty, string.Empty, 0, string.Empty, message);

    /// <summary>A part that reads many children of <paramref name="slot"/>.</summary>
    /// <param name="slot">The many-valued child slot to fill.</param>
    /// <param name="separator">What stands between two entries, or empty for none.</param>
    /// <param name="minimum">How many entries the text has to hold at least.</param>
    public static ImportPart Repeat(string slot, string separator, int minimum) =>
        new(ImportPartKind.Repeat, string.Empty, slot, separator, minimum);

    /// <summary>The token class a capture reads, or empty when it runs to the next literal.</summary>
    public string Token => Kind == ImportPartKind.Capture ? Text : string.Empty;

    public override string ToString() => Kind switch
    {
        ImportPartKind.Literal => $"literal \"{Text}\"",
        ImportPartKind.Capture => $"capture {Slot}",
        ImportPartKind.Nest => $"nest {Slot}",
        _ => $"repeat {Slot}",
    };
}
