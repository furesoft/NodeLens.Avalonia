using Projectional.Core;
using Projectional.Language;

namespace Projectional.Behaviours;

/// <summary>How the behaviour language is drawn: a header, one function per line, and bodies that read as sentences.</summary>
/// <remarks>
/// Every part fits on one line, so a function is one row and a document reads like the definition it is. The root
/// starts one indent in from the page edge and its functions hang one further in, which is the shape every definition
/// language uses (docs/architecture/declared-editor.md).
/// </remarks>
public static class BehaviourNotations
{
    private const string NoFunctions = "<<no functions>>";

    /// <summary>The notation registry of the behaviour language.</summary>
    public static NotationRegistry Registry { get; } = Create(BehaviourLanguage.Concepts);

    /// <summary>The notation registry of <paramref name="concepts"/>.</summary>
    /// <param name="concepts">The concepts to register notations against.</param>
    public static NotationRegistry Create(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        AddTo(notations);

        return notations;
    }

    /// <summary>Adds the language's notations to <paramref name="notations"/>.</summary>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(BehaviourConceptKeys.NameOfPath, ed => ed.Line(
            ed.Keyword("name"),
            ed.Space,
            ed.Keyword("of"),
            ed.Space,
            ed.ChildInline(BehaviourConceptKeys.PathSlot)));

        notations.Add(BehaviourConceptKeys.IndexOfPath, ed => ed.Line(
            ed.Keyword("index"),
            ed.Space,
            ed.Keyword("of"),
            ed.Space,
            ed.ChildInline(BehaviourConceptKeys.PathSlot)));

        notations.Add(BehaviourConceptKeys.PathSelf, ed => ed.Line(ed.Keyword("self"), ed.WhenFilled(BehaviourConceptKeys.NextSlot, ed.Punct("."), ed.ChildInline(BehaviourConceptKeys.NextSlot))));

        notations.Add(BehaviourConceptKeys.PathParent, ed => ed.Line(ed.Keyword("parent"), ed.WhenFilled(BehaviourConceptKeys.NextSlot, ed.Punct("."), ed.ChildInline(BehaviourConceptKeys.NextSlot))));

        notations.Add(BehaviourConceptKeys.PathChild, ed => ed.Line(
            ed.Keyword("child"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot),
            ed.WhenFilled(BehaviourConceptKeys.NextSlot, ed.Punct("."), ed.ChildInline(BehaviourConceptKeys.NextSlot))));

        notations.Add(BehaviourConceptKeys.PathReference, ed => ed.Line(
            ed.Keyword("reference"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot),
            ed.WhenFilled(BehaviourConceptKeys.NextSlot, ed.Punct("."), ed.ChildInline(BehaviourConceptKeys.NextSlot))));

        notations.Add(BehaviourConceptKeys.PathNextSibling, ed => ed.Line(ed.Keyword("nextSibling"), ed.WhenFilled(BehaviourConceptKeys.NextSlot, ed.Punct("."), ed.ChildInline(BehaviourConceptKeys.NextSlot))));

        notations.Add(BehaviourConceptKeys.PathPreviousSibling, ed => ed.Line(ed.Keyword("previousSibling"), ed.WhenFilled(BehaviourConceptKeys.NextSlot, ed.Punct("."), ed.ChildInline(BehaviourConceptKeys.NextSlot))));

        notations.Add(BehaviourConceptKeys.Document, ed => ed.Sequence(
            ed.Indent(ed.Sequence(
                ed.Line(
                    ed.Keyword("behaviour"),
                    ed.Space,
                    ed.Keyword("for"),
                    ed.Space,
                    ed.Property(BehaviourConceptKeys.ConceptSlot)),
                ed.Indent(ed.Children(BehaviourConceptKeys.FunctionsSlot, NoFunctions, stacked: true))))));

        notations.Add(BehaviourConceptKeys.Function, ed => ed.Line(
            ed.Property(BehaviourConceptKeys.KindSlot),
            ed.Punct(":"),
            ed.Space,
            ed.ChildInline(BehaviourConceptKeys.BodySlot)));

        notations.Add(BehaviourConceptKeys.Literal, ed => ed.Line(
            ed.Keyword("literal"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.TextSlot)));

        notations.Add(BehaviourConceptKeys.Property, ed => ed.Line(
            ed.Keyword("property"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot)));

        notations.Add(BehaviourConceptKeys.Child, ed => ed.Line(
            ed.Keyword("child"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot)));

        notations.Add(BehaviourConceptKeys.Reference, ed => ed.Line(
            ed.Keyword("reference"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot)));

        notations.Add(BehaviourConceptKeys.ResolveInfo, ed => ed.Line(
            ed.Keyword("resolveInfo"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot)));

        notations.Add(BehaviourConceptKeys.Join, ed => ed.Line(
            ed.Keyword("join"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SeparatorSlot),
            ed.Space,
            ed.Punct("("),
            ed.InlineCollection(BehaviourConceptKeys.PartsSlot, ed.Punct(",")),
            ed.Punct(")")));

        notations.Add(BehaviourConceptKeys.Fallback, ed => ed.Line(
            ed.Keyword("fallback"),
            ed.Space,
            ed.Punct("("),
            ed.InlineCollection(BehaviourConceptKeys.PartsSlot, ed.Punct(",")),
            ed.Punct(")")));

        notations.Add(BehaviourConceptKeys.Children, ed => ed.Line(
            ed.Keyword("children"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot),
            ed.Space,
            ed.Keyword("with"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SeparatorSlot)));

        notations.Add(BehaviourConceptKeys.WhenFilled, ed => ed.Line(
            ed.Keyword("whenFilled"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot),
            ed.Space,
            ed.Punct("("),
            ed.ChildInline(BehaviourConceptKeys.PartSlot),
            ed.Punct(")")));

        notations.Add(BehaviourConceptKeys.Choose, ed => ed.Line(
            ed.Keyword("choose"),
            ed.Space,
            ed.Property(BehaviourConceptKeys.SlotSlot),
            ed.Space,
            ed.Punct("="),
            ed.Space,
            ed.Property(BehaviourConceptKeys.ValueSlot),
            ed.Space,
            ed.Punct("?"),
            ed.Space,
            ed.ChildInline(BehaviourConceptKeys.ThenSlot),
            ed.Space,
            ed.Punct(":"),
            ed.Space,
            ed.ChildInline(BehaviourConceptKeys.OtherwiseSlot)));
    }
}
