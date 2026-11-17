using Projectional.Core;
using Projectional.Language;

namespace MyLanguage;

/// <summary>The concepts of this language, and nothing about how they look.</summary>
/// <remarks>
/// <para>
/// A language is data: concepts, their slots, and the places they may stand. Presentation is
/// <see cref="MyLanguageNotations"/>' job, because a definition that mixes the domain with the drawing cannot be
/// reviewed.
/// </para>
/// <para>
/// Name the concepts after the language, not after the sample: the key is what a stored document says, so renaming it
/// later is a migration rather than a cleanup.
/// </para>
/// </remarks>
public static class MyLanguageConcepts
{
    /// <summary>The root: a titled list.</summary>
    public const string Board = "MyLanguage.Board";

    /// <summary>One entry of that list.</summary>
    public const string Item = "MyLanguage.Item";

    /// <summary>The slot that holds the entries, named once so the notation cannot misspell it.</summary>
    public const string ItemsSlot = "items";

    /// <summary>The registry every other layer reads the language from.</summary>
    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Board,
            [
                new PropertySlot("title", PropertyType.String, "untitled", description: "What this board is called."),
                new ChildSlot(ItemsSlot, Item),
            ],
            nameSlot: "title",
            description: "A titled list of items."),

        new ConceptDeclaration(
            Item,
            [new PropertySlot("text", PropertyType.String, description: "What this item says.")]),
    ]);

    /// <summary>A project whose language is this one.</summary>
    public static Project NewProject() => new(Concepts);
}