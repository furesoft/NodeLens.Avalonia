using Projectional.Language;
using Projectional.Samples;

namespace Projectional.Screenshots;

/// <summary>Everything a shot needs to draw and edit one language.</summary>
/// <remarks>
/// The three example languages are separate registries with their own emphasis and typing rules, so a shot cannot
/// borrow the sample's. Bundling them keeps "which language is this picture of" one value rather than three
/// arguments that could be mixed up, and the sample language is the default because most shots are of it.
/// </remarks>
/// <param name="Notations">The notation registry the document is projected with.</param>
/// <param name="Emphasis">What the language decorates, if anything.</param>
/// <param name="Typing">What the language says a keystroke claims.</param>
internal sealed record LanguageSurface(
    NotationRegistry Notations,
    ICellEmphasis Emphasis,
    ITypingRules Typing)
{
    /// <summary>The sample rules language, which the feature shots are drawn with.</summary>
    internal static LanguageSurface Sample { get; } = new(
        RulesLanguage.CreateNotations(RulesLanguage.Concepts),
        RulesLanguage.Emphasis,
        RulesLanguage.TypingRules);

    /// <summary>A language that decorates nothing and claims no keystroke.</summary>
    /// <param name="notations">The notation registry to draw with.</param>
    internal static LanguageSurface Plain(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        return new LanguageSurface(notations, NoEmphasis.Instance, NoTyping.Instance);
    }
}
