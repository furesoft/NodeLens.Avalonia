using Projectional.Behaviours;
using Projectional.Core;
using Projectional.Language;
using Projectional.Languages;
using Projectional.Toolkit;

namespace Projectional.LanguageDocs;

/// <summary>What each concept of a language is called, drawn with the language it is written in.</summary>
/// <remarks>
/// The same argument as <see cref="RuleDrawings"/>: a behaviour is a model, so a page can show the definition instead of
/// describing it - and the definition is the half a language author copies from. The notations are the behaviour
/// language's own, not the documented language's, because a document is written in
/// <see cref="BehaviourLanguage"/> whatever language its concept belongs to.
/// </remarks>
internal static class BehaviourDrawings
{
    /// <summary>The behaviour documents of <paramref name="language"/>, drawn and ordered by name.</summary>
    internal static IReadOnlyList<DocumentDrawing> Of(LanguageProject language)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (language.BehaviourDocuments.Count == 0)
        {
            return [];
        }

        return
        [
            .. language.BehaviourDocuments
                .Select(model => Draw(model, BehaviourNotations.Registry))
                .OrderBy(drawing => drawing.Name, StringComparer.Ordinal),
        ];
    }

    /// <summary>Draws one document, or records why it could not be drawn.</summary>
    private static DocumentDrawing Draw(Model model, NotationRegistry notations)
    {
        var name = model.Name ?? "behaviour";

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
