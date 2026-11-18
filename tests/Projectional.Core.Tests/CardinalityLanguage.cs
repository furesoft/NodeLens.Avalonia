using Projectional.Core;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// A language whose two child slots are the two halves of one range: a list that requires an element, and a single
/// child that may stay empty - the same storage shapes a <c>0..1</c> and a <c>1..n</c> spell.
/// </summary>
/// <remarks>
/// A fixture of its own rather than a change to <see cref="TestLanguage"/>: the shared language is what the model
/// tests are written against, and giving it a required list would make every one of them a document with a hole.
/// </remarks>
internal static class CardinalityLanguage
{
    public const string Holder = "Cardinality.Holder";
    public const string Item = "Cardinality.Item";

    /// <summary>A list that promises at least one item (<c>1..n</c>).</summary>
    public const string RequiredSlot = "items";

    /// <summary>A single child that may stay empty (<c>0..1</c>).</summary>
    public const string OptionalSlot = "extra";

    public static ConceptRegistry Registry { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(
            Holder,
            [
                new ChildSlot(RequiredSlot, Item, Cardinality.Many, minimum: 1),
                new ChildSlot(OptionalSlot, Item, Cardinality.One, minimum: 0),
            ],
            description: "A list that needs an item and a slot that does not."),

        new ConceptDeclaration(
            Item,
            [new PropertySlot("name", PropertyType.String, "unnamed")],
            nameSlot: "name"),
    ]);

    public static NotationRegistry Notations { get; } = Create();

    public static Model NewModel() => NewModel(out _);

    public static Model NewModel(out Node holder)
    {
        var model = new Project(Registry).NewModel("Cardinality");

        holder = model.NewNode(Holder);

        return model;
    }

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(Registry);

        notations.Add(Holder, ed => ed.Sequence(
            ed.Line(ed.Keyword("holder", TextEmphasis.Bold)),
            ed.Indent(ed.Children(RequiredSlot, "<< no items >>")),
            ed.Line(ed.Keyword("extra"), ed.Space, ed.ChildInline(OptionalSlot, "<< none >>"))));

        notations.Add(Item, ed => ed.Line(ed.Keyword("item"), ed.Space, ed.Property("name")));

        return notations;
    }
}
