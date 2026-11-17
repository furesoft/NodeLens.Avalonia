using Projectional.Language;

namespace MyLanguage;

/// <summary>How the concepts of this language are drawn, and nothing about what they mean.</summary>
/// <remarks>
/// Every concrete concept needs a notation: a concept without one renders as a missing-notation cell, which is the one
/// state a user cannot work around. A language is written concept by concept, so a missing one shows up as that cell
/// rather than as an exception - but a language that ships is a language whose concepts are all drawn.
/// </remarks>
public static class MyLanguageNotations
{
    /// <summary>The registry the editor projects the model through.</summary>
    public static NotationRegistry Registry { get; } = Create();

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(MyLanguageConcepts.Concepts);

        notations.Add(MyLanguageConcepts.Board, ed => ed.Sequence(
            ed.Line(ed.Keyword("board"), ed.Space, ed.Property("title")),
            ed.Indent(ed.Children(MyLanguageConcepts.ItemsSlot, stacked: true))));

        notations.Add(MyLanguageConcepts.Item, ed => ed.Line(
            ed.Punct("-"),
            ed.Space,
            ed.Property("text")));

        return notations;
    }
}