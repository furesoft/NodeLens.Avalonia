using Projectional.Language;

namespace Projectional.Structures.Notation;

/// <summary>
/// How a language definition is drawn, in the shape MPS writes a structure model: a header that says what the
/// concept is and what it inherits, and a body in braces with a block of options and one section per kind of slot.
/// </summary>
/// <remarks>
/// <para>
/// The shape is worth copying because it is the shape the problem has. A reader sees what the concept <em>is</em> and
/// what it extends in the header, what it allows in the options, and what it holds in three named sections - which
/// is why a definition can be reviewed without its presentation, and why the sections are collections in the model
/// rather than a grouping the renderer invents.
/// </para>
/// <para>
/// The two lists in the header are inline collections inside a <see cref="WhenFilledNotationCell"/>, so an empty one
/// is not drawn at all: <c>concept Rule extends Condition {</c> says what is true of this concept, and a concept
/// that extends nothing does not mention extends. The price is paid in the same file - an empty slot has no caret
/// stop, so the first entry is written with <c>StructureIntentions.InheritFrom</c>/<c>ImplementInterface</c> instead
/// of with the caret.
/// </para>
/// <para>
/// An empty section names the kind it will create - <c>&lt;&lt;property&gt;&gt;</c>, <c>&lt;&lt;child&gt;&gt;</c>,
/// <c>&lt;&lt;reference&gt;&gt;</c>, and <c>&lt;&lt;symbol&gt;&gt;</c> for an enumeration. A section nobody has
/// filled is a state to see, and a heading with nothing under it reads as a mistake; one shared placeholder for all
/// three would say nothing about which slot Enter is about to fill.
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
    /// <summary>
    /// The empty state of one of the header's two lists. Unreachable by construction - a list is only drawn while it
    /// is filled - and kept because a cell that can be empty has to say something, so the impossible state reads as
    /// the list it is rather than as a blank.
    /// </summary>
    private const string NoInheritance = "<<>>";

    private const string NoProperties = "<<property>>";
    private const string NoChildren = "<<child>>";
    private const string NoReferences = "<<reference>>";
    private const string NoSymbols = "<<symbol>>";

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
        // The two lists are drawn only while they hold something, which is what makes a header read as a sentence
        // about this concept: `concept Rule extends Condition {` says what it extends, and a concept that extends
        // nothing does not mention extends at all. The price is that the first entry has nowhere to be typed -
        // `StructureIntentions.InheritFrom`/`ImplementInterface` are the way in, exactly as "make abstract" is for
        // the word that has no cell either.
        //
        // The comma between the two lists sits inside the second one and is conditioned on the first: `extends A
        // implements B` has none, and a comma that stayed behind while its list vanished would dangle.
        notations.Add(StructureConceptKeys.Concept, ed => ed.Sequence(
            ed.Line(
                ed.Conditional(StructureLanguage.IsInterfaceProperty, "true", ed.Keyword("interface"), ed.Space),
                ed.Conditional(
                    StructureLanguage.IsInterfaceProperty,
                    "false",
                    ed.Conditional(StructureLanguage.IsAbstractProperty, "true", ed.Keyword("abstract"), ed.Space)),
                ed.Keyword("concept"),
                ed.Space,
                ed.Property(StructureLanguage.KeySlot),
                ed.WhenFilled(
                    StructureLanguage.SuperTypeSlot,
                    ed.Space,
                    ed.Keyword("extends"),
                    ed.Space,
                    ed.InlineCollection(StructureLanguage.SuperTypeSlot, ed.Punct(","), NoInheritance)),
                ed.WhenFilled(
                    StructureLanguage.ImplementsSlot,
                    ed.WhenFilled(StructureLanguage.SuperTypeSlot, ed.Punct(",")),
                    ed.Space,
                    ed.Keyword("implements"),
                    ed.Space,
                    ed.InlineCollection(StructureLanguage.ImplementsSlot, ed.Punct(","), NoInheritance)),
                ed.Space,
                ed.Punct("{")),
            ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("offered in completion:"), ed.Space, ed.Property("isOfferedInCompletion")),
                ed.Line(ed.Keyword("named by:"), ed.Space, ed.Property("nameSlot")),
                ed.Line(ed.Keyword("collapsed by:"), ed.Space, ed.Property("collapseSlot")),
                ed.Line(ed.Keyword("named by rule:"), ed.Space, ed.Property("nameRule")),
                ed.Line(ed.Keyword("described as:"), ed.Space, ed.Property("description")),
                ed.Line(ed.Keyword("properties:")),
                ed.Indent(ed.Children(StructureLanguage.PropertiesSlot, NoProperties, stacked: true)),
                ed.Line(ed.Keyword("children:")),
                ed.Indent(ed.Children(StructureLanguage.ChildrenSlot, NoChildren, stacked: true)),
                ed.Line(ed.Keyword("references:")),
                ed.Indent(ed.Children(StructureLanguage.ReferencesSlot, NoReferences, stacked: true)))),
            ed.Line(ed.Punct("}"))));

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
            ed.Indent(ed.Children("symbols", NoSymbols, stacked: true))));

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
