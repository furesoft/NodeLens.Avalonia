using Projectional.Core;
using Projectional.EvaluationRules;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Toolkit;

namespace Projectional.LanguageDocs;

/// <summary>The rules a language states, drawn with the language they are written in.</summary>
/// <remarks>
/// <para>
/// A rule is a model like every other definition, so a page can show it rather than only describe it: the sentence a rule
/// carries says what it computes, and the drawing says how it says so. Without this the site documented the *effect* of a
/// rule and never the rule, which is the half a language author copies from.
/// </para>
/// <para>
/// The notations are the rule language's own, not the documented language's: a rule is written in
/// <see cref="EvaluationRuleLanguage"/> whatever language it is about, so drawing it with the language it describes would
/// ask for a notation no concept of it has - and a rule page that silently showed nothing would be worse than none.
/// </para>
/// </remarks>
internal static class RuleDrawings
{
    /// <summary>The rule documents of <paramref name="language"/>, drawn and ordered by name.</summary>
    internal static IReadOnlyList<DocumentDrawing> Of(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (language.EvaluationDocuments.Count == 0)
        {
            return [];
        }

        var notations = EvaluationRuleLanguage.CreateNotations(EvaluationRuleLanguage.Concepts);

        return
        [
            .. language.EvaluationDocuments
                .Select(model => Draw(model, notations))
                .OrderBy(drawing => drawing.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>Draws one rule, or records why it could not be drawn.</summary>
    private static DocumentDrawing Draw(Model model, NotationRegistry notations)
    {
        var name = model.Name ?? "rule";

        try
        {
            return new DocumentDrawing(
                name,
                HighlightedText.Of(LanguageSnapshot.Runs(model, notations, ExampleDocuments.Width, annotations: false)),
                Refusal: null);
        }
        catch (Exception exception) when (exception is ConceptDefinitionException or NotationException)
        {
            return new DocumentDrawing(name, Html: string.Empty, exception.Message);
        }
    }
}
