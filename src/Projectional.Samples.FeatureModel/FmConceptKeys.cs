namespace Projectional.Samples.FeatureModel;

/// <summary>The keys of the concepts <see cref="FeatureModelLanguage"/> declares.</summary>
public static class FmConceptKeys
{
    /// <summary>A model: its features and the constraints between them.</summary>
    public const string Model = "Fm.Model";

    /// <summary>One feature, with a name and whether the current configuration selects it.</summary>
    public const string Feature = "Fm.Feature";

    /// <summary>The abstract rule about two features that the two kinds of constraint share.</summary>
    public const string Constraint = "Fm.Constraint";

    /// <summary>A constraint that a selected feature drags another one in with it.</summary>
    public const string Requires = "Fm.Requires";

    /// <summary>A constraint that a selected feature keeps another one out.</summary>
    public const string Excludes = "Fm.Excludes";
}
