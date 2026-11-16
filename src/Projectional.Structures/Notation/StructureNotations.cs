using Projectional.Language;

namespace Projectional.Structures.Notation;

/// <summary>
/// How a language definition is drawn: one line per concept, one per slot, nothing clever.
/// </summary>
/// <remarks>
/// <para>
/// The provisional shape of slice P1, and provisional in a specific way: it reads like the C# a language author
/// would have written - <c>concept Rules.Rule</c>, then its slots indented under it - rather than like MPS'
/// angle-bracket block language. That is deliberate, because the shape of a definition should be decided by
/// someone reading definitions, and P4 is where it becomes MPS-shaped.
/// </para>
/// <para>
/// It is a real notation all the same, and the tests render it: a definition language that needed a rendering
/// exception would not be a language, and a definition nobody can look at is a data structure.
/// </para>
/// </remarks>
public static class StructureNotations
{
    /// <summary>Registers the notation of every concept of the structure language.</summary>
    /// <param name="notations">The registry to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        var ed = NotationEditor.Create();

        notations.Add(StructureConceptKeys.Language, ed => ed.Sequence(
            ed.Children(StructureLanguage.EnumerationSlot, "<no enumerations>"),
            ed.Children(StructureLanguage.ConceptSlot, "<no concepts>")));

        notations.Add(StructureConceptKeys.Concept, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("concept"),
                ed.Space,
                ed.Property(StructureLanguage.KeySlot),
                ed.Space,
                ed.Keyword("abstract"),
                ed.Punct("="),
                ed.Property("isAbstract"),
                ed.Space,
                ed.Keyword("offered"),
                ed.Punct("="),
                ed.Property("isOfferedInCompletion")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("named by"), ed.Space, ed.Property("nameSlot")),
                ed.Line(ed.Keyword("described as"), ed.Space, ed.Property("description")),
                ed.Children(StructureLanguage.SuperTypeSlot, "<no super concepts>"),
                ed.Children(StructureLanguage.SlotSlot, "<no slots>")))));

        notations.Add(StructureConceptKeys.SuperType, ed => ed.Line(
            ed.Keyword("extends"),
            ed.Space,
            ed.Property("conceptKey")));

        // Abstract on purpose: a slot is written as one of the three kinds, and a bare one would be a slot with
        // nothing to say. The notation exists so the abstract concept can still be drawn if it ever appears.
        notations.Add(StructureConceptKeys.Slot, ed => ed.Line(
            ed.Keyword("slot"),
            ed.Space,
            ed.Property("name")));

        // The optional fields get their own lines, like the concept's do: an empty property holds no cells, so a
        // "default ... as ..." written inline leaves gaps between the words that are still there.
        notations.Add(StructureConceptKeys.PropertyDecl, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("property"),
                ed.Space,
                ed.Property("name"),
                ed.Space,
                ed.Punct(":"),
                ed.Space,
                ed.Property("typeName")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("default"), ed.Space, ed.Property("defaultValue")),
                ed.Line(ed.Keyword("described as"), ed.Space, ed.Property("description"))))));

        notations.Add(StructureConceptKeys.ChildDecl, ed => ed.Line(
            ed.Keyword("child"),
            ed.Space,
            ed.Property("name"),
            ed.Space,
            ed.Punct(":"),
            ed.Space,
            ed.Property("childConceptKey"),
            ed.Space,
            ed.Keyword("cardinality"),
            ed.Space,
            ed.Property("cardinality")));

        notations.Add(StructureConceptKeys.ReferenceDecl, ed => ed.Line(
            ed.Keyword("reference"),
            ed.Space,
            ed.Property("name"),
            ed.Space,
            ed.Punct(":"),
            ed.Space,
            ed.Property("targetConceptKey"),
            ed.Space,
            ed.Keyword("own subtree"),
            ed.Space,
            ed.Property("allowOwnSubtree")));

        notations.Add(StructureConceptKeys.Enumeration, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("enum"),
                ed.Space,
                ed.Property(StructureLanguage.KeySlot)),
            ed.Indent(ed.Children("symbols", "<no symbols>"))));

        notations.Add(StructureConceptKeys.EnumSymbol, ed => ed.Line(ed.Property("symbol")));
    }

    /// <summary>A registry holding only the structure language, which is what a definition is drawn with.</summary>
    public static NotationRegistry Create()
    {
        var notations = new NotationRegistry(StructureLanguage.Concepts);

        AddTo(notations);

        return notations;
    }
}
