using Projectional.Language;
using Projectional.Languages;
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
/// <param name="Types">
/// The language's type rules, which a notation's <c>typeof</c> cell draws; nothing when the language declares no
/// types, and the annotation then reads <c>unknown</c>.
/// </param>
internal sealed record LanguageSurface(
    NotationRegistry Notations,
    ICellEmphasis Emphasis,
    ITypingRules Typing,
    TypeRegistry? Types = null)
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

    /// <summary>A language written as a project, with everything a document it opens is drawn and typed by.</summary>
    /// <remarks>
    /// Read off the opened project instead of being handed in one member at a time: a declared language's notations,
    /// typing rules and types are all lowered from the same folder, so a picture that named two of them and forgot the
    /// third would draw a document the language cannot produce.
    /// </remarks>
    /// <param name="language">The project that was opened.</param>
    internal static LanguageSurface Of(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return new LanguageSurface(language.Notations, NoEmphasis.Instance, language.Typing, language.Types);
    }
}
