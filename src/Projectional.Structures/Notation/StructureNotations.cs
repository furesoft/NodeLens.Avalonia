using Projectional.Language;

namespace Projectional.Structures.Notation;

/// <summary>
/// How a language definition is drawn, in the shape MPS writes a structure model: a header, a block of options, and
/// one section per kind of slot, with every declaration on a line of its own.
/// </summary>
/// <remarks>
/// <para>
/// The shape is worth copying because it is the shape the problem has. A reader sees what the concept <em>is</em> in
/// the header, what it allows in the options, and what it holds in three named sections - which is why a definition
/// can be reviewed without its presentation, and why the sections are collections in the model rather than a
/// grouping the renderer invents.
/// </para>
/// <para>
/// Empty sections show <c>&lt;&lt; ... &gt;&gt;</c>, MPS' placeholder, rather than a bare heading: a section nobody
/// has filled is a state to see, and a heading with nothing under it reads as a mistake.
/// </para>
/// <para>
/// A slot declaration carries its own kind and closes where it ends - <c>&lt;&lt;property name : string ...
/// &gt;&gt;</c>, <c>&lt;&lt;child steps : Rules.Step[1..n]&gt;&gt;</c>, <c>&lt;&lt;reference condition :
/// Rules.Condition ... &gt;&gt;</c>. The marker is what makes a list of declarations readable: a property has three
/// option lines under it, and without a visible end the reader cannot tell where one declaration stops and the next
/// begins. It is also the word MPS uses for the same three things.
/// </para>
/// <para>
/// Every collection here stacks, because a definition is read one declaration per line. The layout draws a
/// collection inline by default (that is what a notation asks for with a separator), so the stacking is a decision
/// this file makes rather than something the renderer would have guessed.
/// </para>
/// <para>
/// <c>abstract</c> is a word in the header rather than an option, which is MPS' spelling and the better reading of
/// the same fact: abstractness is part of what the concept <em>is</em>, not one of its settings. It is drawn by a
/// <see cref="ConditionalNotationCell"/>, so a concrete concept shows <c>concept Rules.Rule</c> and nothing else -
/// which is also why the value is not a cell the user can type into, and why toggling it is an intention.
/// </para>
/// </remarks>
public static class StructureNotations
{
    private const string NoEntries = "<< ... >>";

    /// <summary>Registers the notation of every concept of the structure language.</summary>
    /// <param name="notations">The registry to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        var ed = NotationEditor.Create();

        // `abstract concept Rule` rather than an option, which is how MPS writes it and how the word is read: as
        // part of what the concept is, not as one of its settings. The word exists in the notation always and in
        // the drawing only while the flag is true - a false one draws nothing at all, not an empty cell.
        //
        // `interface` replaces `abstract` instead of joining it, and the nested conditional is what says so: a
        // conditional holds inline cells, and a conditional is an inline cell, so "abstract unless interface" is
        // two of them. Drawing both would say the same thing twice, because an interface is abstract by
        // definition.
        notations.Add(StructureConceptKeys.Concept, ed => ed.Sequence(
            ed.Line(
                ed.Conditional(StructureLanguage.IsInterfaceProperty, "true", ed.Keyword("interface"), ed.Space),
                ed.Conditional(
                    StructureLanguage.IsInterfaceProperty,
                    "false",
                    ed.Conditional(StructureLanguage.IsAbstractProperty, "true", ed.Keyword("abstract"), ed.Space)),
                ed.Keyword("concept"),
                ed.Space,
                ed.Property(StructureLanguage.KeySlot)),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("offered in completion:"), ed.Space, ed.Property("isOfferedInCompletion")),
                ed.Line(ed.Keyword("named by:"), ed.Space, ed.Property("nameSlot")),
                ed.Line(ed.Keyword("collapsed by:"), ed.Space, ed.Property("collapseSlot")),
                ed.Line(ed.Keyword("described as:"), ed.Space, ed.Property("description")),
                ed.Line(ed.Keyword("extends:")),
                ed.Indent(ed.Children(StructureLanguage.SuperTypeSlot, NoEntries, stacked: true)),
                ed.Line(ed.Keyword("implements:")),
                ed.Indent(ed.Children(StructureLanguage.ImplementsSlot, NoEntries, stacked: true)),
                ed.Line(ed.Keyword("properties:")),
                ed.Indent(ed.Children(StructureLanguage.PropertiesSlot, NoEntries, stacked: true)),
                ed.Line(ed.Keyword("children:")),
                ed.Indent(ed.Children(StructureLanguage.ChildrenSlot, NoEntries, stacked: true)),
                ed.Line(ed.Keyword("references:")),
                ed.Indent(ed.Children(StructureLanguage.ReferencesSlot, NoEntries, stacked: true))))));

        notations.Add(StructureConceptKeys.SuperType, ed => ed.Line(ed.Property("conceptKey")));

        notations.Add(StructureConceptKeys.Slot, ed => ed.Line(
            ed.Keyword("slot"),
            ed.Space,
            ed.Property("name")));

        notations.Add(StructureConceptKeys.PropertyDecl, ed => ed.Sequence(
            ed.Line(
                ed.Punct("<<"),
                ed.Keyword("property"),
                ed.Space,
                ed.Property("name"),
                ed.Space,
                ed.Punct(":"),
                ed.Space,
                ed.Property("typeName")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("default:"), ed.Space, ed.Property("defaultValue")),
                ed.Line(ed.Keyword("described as:"), ed.Space, ed.Property("description")),
                ed.Line(
                    ed.Keyword("described when filled:"),
                    ed.Space,
                    ed.Property("showDescriptionWhenFilled"),
                    ed.Punct(">>"))))));

        notations.Add(StructureConceptKeys.ChildDecl, ed => ed.Line(
            ed.Punct("<<"),
            ed.Keyword("child"),
            ed.Space,
            ed.Property("name"),
            ed.Space,
            ed.Punct(":"),
            ed.Space,
            ed.Property("childConceptKey"),
            ed.Punct("["),
            ed.Property("cardinality"),
            ed.Punct("]"),
            ed.Punct(">>")));

        notations.Add(StructureConceptKeys.ReferenceDecl, ed => ed.Sequence(
            ed.Line(
                ed.Punct("<<"),
                ed.Keyword("reference"),
                ed.Space,
                ed.Property("name"),
                ed.Space,
                ed.Punct(":"),
                ed.Space,
                ed.Property("targetConceptKey")),
            ed.Indent(ed.Sequence(
                ed.Line(
                    ed.Keyword("own subtree:"),
                    ed.Space,
                    ed.Property("allowOwnSubtree"),
                    ed.Punct(">>"))))));

        notations.Add(StructureConceptKeys.Enumeration, ed => ed.Sequence(
            ed.Line(ed.Keyword("enum"), ed.Space, ed.Property(StructureLanguage.KeySlot)),
            ed.Indent(ed.Children("symbols", NoEntries, stacked: true))));

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
