using Projectional.Core;
using Projectional.Editing;
using Projectional.Language;

namespace Projectional.Core.Tests;

/// <summary>
/// A language whose slots are declared over abstract concepts, in both the shapes that matter: a set
/// with something in it and a set with nothing in it.
/// </summary>
/// <remarks>
/// <see cref="TestLanguage"/> has no abstract concept on purpose - the model tests want plain nodes -
/// so the two behaviours that only exist for abstract slots get their own little language instead of
/// growing the shared fixture a slot that most tests would have to ignore.
/// </remarks>
internal static class AbstractSlotLanguage
{
    public const string Holder = "Abs.Holder";
    public const string Value = "Abs.Value";
    public const string Number = "Abs.Number";
    public const string Sealed = "Abs.Sealed";
    public const string SealedBelow = "Abs.SealedBelow";

    public static ConceptRegistry Concepts { get; } = ConceptRegistry.Create(
    [
        new ConceptDeclaration(Holder, [
            new ChildSlot("value", Value, Cardinality.One),
            new ChildSlot("sealed", Sealed, Cardinality.One),
        ]),

        new ConceptDeclaration(Value, isAbstract: true),

        new ConceptDeclaration(Number, [new PropertySlot("number", PropertyType.Integer)], [Value]),

        new ConceptDeclaration(Sealed, isAbstract: true),
        new ConceptDeclaration(SealedBelow, superConcepts: [Sealed], isAbstract: true),
    ]);

    public static NotationRegistry Notations { get; } = Create();

    /// <summary>A holder as the only root of a fresh document.</summary>
    public static Node NewHolder()
    {
        var model = new Project(Concepts).NewModel("Abstract");
        var holder = model.NewNode(Holder);
        model.AddRoot(holder);

        return holder;
    }

    /// <summary>Opens <paramref name="document"/> for editing.</summary>
    public static EditorSession Open(Node document) => new(document, Notations);

    private static NotationRegistry Create()
    {
        var notations = new NotationRegistry(Concepts);

        notations.Add(Holder, ed => ed.Sequence(
            ed.Line(ed.Keyword("holder"), ed.Space, ed.ChildInline("value")),
            ed.Line(ed.Keyword("sealed"), ed.Space, ed.ChildInline("sealed"))));

        notations.Add(Number, ed => ed.Line(ed.Property("number")));

        return notations;
    }
}
