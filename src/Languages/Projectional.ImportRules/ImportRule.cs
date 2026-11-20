namespace Projectional.ImportRules;

/// <summary>What one concept looks like in the text: a concept key and the parts that read it.</summary>
/// <param name="ConceptKey">The concept this rule reads, which is the key the rule document names.</param>
/// <param name="Parts">The parts, in the order the text has them.</param>
public sealed record ImportRule(string ConceptKey, IReadOnlyList<ImportPart> Parts)
{
    /// <summary>The text this rule starts with, or empty when it needs a value first.</summary>
    /// <remarks>
    /// The anchor is what makes the choice between rules decidable: the importer never tries one rule and leaves it
    /// unparsed, it looks at what is written and takes the one rule whose start matches.
    /// </remarks>
    public string Anchor =>
        Parts.Count > 0 && Parts[0].Kind == ImportPartKind.Literal ? Parts[0].Text : string.Empty;

    public override string ToString() => $"{ConceptKey} ({Parts.Count} part(s))";
}
