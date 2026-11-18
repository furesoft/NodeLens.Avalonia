using Projectional.Checks;

namespace Projectional.Samples.FeatureModel.Checks;

/// <summary>The checks a host runs on a feature model: the standard catalogue plus the one only this language has.</summary>
/// <remarks>
/// The standard catalogue is included rather than reimplemented - an unnamed feature, surrounding whitespace and
/// an empty slot are findings about any language - so this type is the one line a language author writes to say
/// what its own checks are. A host asks the set, never the individual checks.
/// </remarks>
public static class FeatureModelChecks
{
    /// <summary>Every check this language offers, in the order their findings are shown.</summary>
    public static CheckSet All { get; } = StandardChecks.All.With(new FeatureNamesCheck());
}
