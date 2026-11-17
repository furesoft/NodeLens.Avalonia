using Projectional.Core;
using Projectional.Language;

namespace Projectional.Toolkit.Tests;

/// <summary>
/// A language written the way a language author writes one: declarations against the toolkit, nothing else.
/// </summary>
/// <remarks>
/// It is deliberately tiny - a titled board holding ordered items - because its job is to prove that the public
/// surface is enough to declare a notation and draw it, not to be an interesting language. Nothing here reaches past
/// <c>Projectional.Toolkit</c>; if something had to, this project would not compile, which is the test.
/// </remarks>
internal static class BoardLanguage
{
    public const string Board = "Toolkit.Board";
    public const string Item = "Toolkit.Item";
    public const string ItemsSlot = "items";

    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Board,
            [
                new PropertySlot("title", PropertyType.String, "untitled"),
                new ChildSlot(ItemsSlot, Item),
            ],
            nameSlot: "title",
            description: "A titled list of items."),

        new ConceptDeclaration(
            Item,
            [new PropertySlot("text", PropertyType.String, description: "What this item says.")]),
    ]);

    public static Project NewProject() => new(Concepts);
}