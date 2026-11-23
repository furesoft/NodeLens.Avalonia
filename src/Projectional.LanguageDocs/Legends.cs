namespace Projectional.LanguageDocs;

/// <summary>The sentences that explain how a page spells a shape, written once so two pages cannot drift apart.</summary>
/// <remarks>
/// A notation and a generator template are both spelled with a slot as its name, and both have to say that a child
/// brings its own text - so the concept page, the notations page and the generators page would otherwise carry three
/// copies of one rule, and the copy edited last would be the one that lies.
/// </remarks>
internal static class Legends
{
    /// <summary>What a notation shape means: a slot is its name, and a child draws its own punctuation.</summary>
    internal static string Notation() =>
        Html.Paragraph(
            "A slot is written as its name, so <name> holds one child or value and <name...> holds a list; further"
            + " indentation is another level. A slot draws in its place whatever the child itself draws, so punctuation"
            + " the child owns - the semicolon a statement ends with - belongs to that statement and is not repeated"
            + " here. This is the shape a document of the concept takes, not a document.");

    /// <summary>What a generator template means: a slot is its name, and a child prints its own text.</summary>
    internal static string Template() =>
        Html.MarkupParagraph(
            "A slot is written as <code>{name}</code> whether it holds a value or a child, and a list as"
            + " <code>{name...}</code>. A child contributes the text of its own template rather than a placeholder,"
            + " so a field that reuses a statement prints that statement including the <code>;</code> it ends with.");
}
