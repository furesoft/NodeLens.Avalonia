using Projectional.Core;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>A language whose nodes can stand for a use: an item that references another item.</summary>
/// <remarks>
/// The shape the refactoring plan calls "a reference plus a node standing for it", which nothing shipped had yet. One
/// concept serves both roles because the distinction is where a node stands and not what it is: an item in <c>defs</c>
/// is a declaration, an item in <c>uses</c> is a use of whatever its <c>target</c> names, and inlining a use puts a copy
/// of the target in its place.
/// </remarks>
internal static class InlineLanguage
{
    public const string Root = "Inline.Root";
    public const string Item = "Inline.Item";

    public const string Definitions = "defs";
    public const string Uses = "uses";
    public const string Target = "target";

    public static ConceptRegistry Registry { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Root,
            [
                new PropertySlot("name", PropertyType.String, "root"),
                new ChildSlot(Definitions, Item),
                new ChildSlot(Uses, Item),
            ],
            nameSlot: "name"),

        new ConceptDeclaration(
            Item,
            [
                new PropertySlot("text", PropertyType.String, "item"),
                new ReferenceSlot(Target, Item),
            ],
            nameSlot: "text"),
    ]);

    /// <summary>How the fixture is drawn, which is what gives it caret places to put an intention on.</summary>
    public static NotationRegistry Notations { get; } = Create();

    public static Model NewModel() => new Project(Registry).NewModel("Inline");

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(Registry);

        notations.Add(Root, ed => ed.Sequence(
            ed.Line(ed.Keyword("root", TextEmphasis.Bold), ed.Space, ed.Property("name")),
            ed.Indent(ed.Children(Definitions, "<< no definitions >>")),
            ed.Indent(ed.Children(Uses, "<< no uses >>"))));

        notations.Add(Item, ed => ed.Line(ed.Property("text")));

        return notations;
    }
}
