using Projectional.Language;

namespace Projectional.Structures.Notation;

/// <summary>
/// How a language definition is drawn, in the shape MPS writes a structure model: a header that says what the
/// concept is and what it inherits, then a block of options and one section per kind of slot, the four groups
/// separated by blank lines.
/// </summary>
/// <remarks>
/// <para>
/// The shape is worth copying because it is the shape the problem has. A reader sees what the concept <em>is</em> and
/// what it extends in the header, what it allows in the options, and what it holds in three named sections - which
/// is why a definition can be reviewed without its presentation, and why the sections are collections in the model
/// rather than a grouping the renderer invents.
/// </para>
/// <para>
/// There are no braces. A concept is a document, and a document does not need a bracket to say where it ends; what it
/// needs is the grouping, which the blank lines between the header, the options and each section give. Indentation
/// is then only used for what is <em>under</em> something - the entries of <c>implements</c>, the rows of a section,
/// and the option lines under a declaration - so a reader sees at a glance which lines belong to which group.
/// </para>
/// <para>
/// The body sits two indents in from the page edge, and the header stands one step further in than the options
/// below it. That is the shape MPS draws, and it is worth the indents: a page that starts on the border reads as
/// clipped, and the distinction between "what the concept is" and "how the framework treats it" becomes a matter of
/// where the lines are rather than of what they say.</para>
/// <para>
/// The two lists of the header are drawn only while they hold something. <c>extends</c> stays inline, because its
/// entries are a sentence: <c>concept Rule extends Condition</c>. <c>implements</c> gets a row of its own with its
/// entries stacked under it, which needs the <em>block</em> form of the conditional
/// (<see cref="WhenFilledBlockNotationCell"/>): a row that drew nothing would still occupy a line, and a concept
/// that implements nothing has no such row at all. The price is paid in the same file - an empty slot has no caret
/// stop, so the first entry is written with <c>StructureIntentions.InheritFrom</c>/<c>ImplementInterface</c> instead
/// of with the caret.
/// </para>
/// <para>
/// An empty section says <c>&lt;&lt; ... &gt;&gt;</c>: something belongs here, and the heading above it already says
/// which kind of thing. The per-kind placeholder said it twice, and the one shared form is what MPS draws.
/// </para>
/// <para>
/// A section's rows all read the same way - <c>name : type</c> for a property, <c>name : Concept[range]</c> for a
/// child, <c>name : Concept</c> for a reference - because the heading above them already says which kind they are.
/// A marker around every row would repeat the heading once per line, and MPS draws them without one for exactly
/// that reason. What a row needs instead of a closing marker is the indentation of the lines that hang under it: a
/// property's <c>default:</c> and <c>described as:</c>, a reference's <c>own subtree:</c>, are one step in from the
/// row they belong to, which is the same rule the entries of <c>implements</c> follow.
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
/// <para>
/// The language root is the one document that is not a definition, so it is drawn for what it is: <c>language Name</c>
/// as its header, then its version and the two lists it stands on, one indent in - the same relation a concept's
/// options have to its header. An empty list says <c>&lt;&lt; ... &gt;&gt;</c> like any other section, and an absent
/// version says <c>&lt;any version&gt;</c>, because asking for nothing is a statement rather than a blank.
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

    private const string NoAlias = "<no alias>";
    private const string NoShortDescription = "<no short description>";

    private const string NoSection = "<< ... >>";
    private const string NoSymbols = "<<symbol>>";
    private const string NoVersion = "<any version>";

    /// <summary>Registers the notation of every concept of the structure language.</summary>
    /// <param name="notations">The registry to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        var ed = NotationEditor.Create();

        // The language root is the one document that is not a definition, so it is drawn as what it is: a header
        // that names the language, then the facts about it - its version and the two lists it stands on - one
        // indent in, the same way a concept's options hang under its header. The empty placeholder says a list is
        // there to be filled, and `version` says "any" because an absent version is a promise of nothing rather
        // than a missing value.
        notations.Add(StructureConceptKeys.Language, ed =>
        {
            var facts = ed.Sequence(
                ed.Line(
                    ed.Keyword("version:"),
                    ed.Space,
                    ed.Property(StructureLanguage.VersionSlot, emptyText: NoVersion)),
                ed.Line(ed.Keyword("depends on:")),
                ed.Indent(ed.Children(StructureLanguage.DependenciesSlot, NoSection, stacked: true)),
                ed.Line(ed.Keyword("extends:")),
                ed.Indent(ed.Children(StructureLanguage.ExtendsSlot, NoSection, stacked: true)));

            return ed.Indent(ed.Indent(ed.Sequence(
                ed.Line(ed.Keyword("language"), ed.Space, ed.Property("name")),
                ed.Indent(facts))));
        });

        // One dependency is one line, because that is what it is: a name and the version asked for, if any. The
        // list above it already says whether it is a dependency or an extension.
        notations.Add(StructureConceptKeys.Dependency, ed => ed.Line(
            ed.Property("name"),
            ed.Space,
            ed.Property(StructureLanguage.VersionSlot, emptyText: NoVersion)));

        // `abstract concept Rule` rather than an option, which is how MPS writes it and how the word is read: as
        // part of what the concept is, not as one of its settings. The word exists in the notation always and in
        // the drawing only while the flag is true - a false one draws nothing at all, not an empty cell.
        //
        // `interface` replaces `abstract` instead of joining it, and the nested conditional is what says so: a
        // conditional holds inline cells, and a conditional is an inline cell, so "abstract unless interface" is
        // two of them. Drawing both would say the same thing twice, because an interface is abstract by
        // definition.
        // The two lists are drawn only while they hold something, which is what makes a header read as a sentence
        // about this concept: `concept Rule extends Condition` says what it extends, and a concept that extends
        // nothing does not mention extends at all. The price is that the first entry has nowhere to be typed -
        // `StructureIntentions.InheritFrom`/`ImplementInterface` are the way in, exactly as "make abstract" is for
        // the word that has no cell either.
        //
        // `implements` has a row of its own rather than a word in the header, because the entries then line up under
        // it and a reader sees a list instead of a comma-separated run. That needs the block form of `WhenFilled`:
        // an unfilled row would leave the blank line it drew, and a concept that implements nothing has no such row
        // at all.
        notations.Add(StructureConceptKeys.Concept, ed => ed.Indent(ed.Indent(ed.Sequence(
            // The body sits two indents in from the page edge and the header one further in, which is the shape MPS
            // draws: the page keeps a margin, the settings about the concept start there, and what the concept *is*
            // stands apart from them.
            ed.Indent(ed.Line(
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
                    ed.InlineCollection(StructureLanguage.SuperTypeSlot, ed.Punct(","), NoInheritance)))),
            ed.WhenFilledBlock(
                StructureLanguage.ImplementsSlot,
                ed.Indent(ed.Horizontal(
                    ed.Line(ed.Keyword("implements"), ed.Space),
                    ed.Children(StructureLanguage.ImplementsSlot, NoSection, stacked: true)))),
            ed.Line(),
            ed.Line(ed.Keyword("instance can be root:"), ed.Space, ed.Property(StructureLanguage.IsRootProperty)),
            ed.Line(ed.Keyword("alias:"), ed.Space, ed.Property(StructureLanguage.AliasProperty, emptyText: NoAlias)),
            ed.Line(
                ed.Keyword("short description:"),
                ed.Space,
                ed.Property("description", emptyText: NoShortDescription)),
            ed.Line(ed.Keyword("offered in completion:"), ed.Space, ed.Property("isOfferedInCompletion")),
            ed.Line(ed.Keyword("named by:"), ed.Space, ed.Property("nameSlot")),
            ed.Line(ed.Keyword("collapsed by:"), ed.Space, ed.Property("collapseSlot")),
            ed.Line(ed.Keyword("named by rule:"), ed.Space, ed.Property("nameRule")),
            ed.Line(),
            ed.Line(ed.Keyword("properties:")),
            ed.Indent(ed.Children(StructureLanguage.PropertiesSlot, NoSection, stacked: true)),
            ed.Line(),
            ed.Line(ed.Keyword("children:")),
            ed.Indent(ed.Children(StructureLanguage.ChildrenSlot, NoSection, stacked: true)),
            ed.Line(),
            ed.Line(ed.Keyword("references:")),
            ed.Indent(ed.Children(StructureLanguage.ReferencesSlot, NoSection, stacked: true))))));

        notations.Add(StructureConceptKeys.SuperType, ed => ed.Line(ed.Property("conceptKey")));

        notations.Add(StructureConceptKeys.Slot, ed => ed.Line(
            ed.Keyword("slot"),
            ed.Space,
            ed.Property("name")));

        // A property reads like a child's row - `name : string` - because the section heading already says the kind,
        // and a marker around every row would repeat it once per line. What the option lines under it need instead
        // of the closing `>>` is the indentation: a line that stands under another one belongs to it, which is the
        // same rule the entries of `implements` follow.
        notations.Add(StructureConceptKeys.PropertyDecl, ed => ed.Sequence(
            ed.Line(
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
                    ed.Property("showDescriptionWhenFilled"))))));

        // A child reads `testMethod : StateMachineTestMethod[0..n]`, which is how MPS writes one: the section heading
        // already says the kind, and the row only has to say the name, the concept and the range - a marker around it
        // would repeat the heading on every line. The indent belongs to the section, not to this row, so that the
        // empty placeholder stands where the rows stand.
        notations.Add(StructureConceptKeys.ChildDecl, ed => ed.Line(
            ed.Property("name"),
            ed.Space,
            ed.Punct(":"),
            ed.Space,
            ed.Property("childConceptKey"),
            ed.Punct("["),
            ed.Property("cardinality"),
            ed.Punct("]")));

        // A reference reads like a child's row as well - `condition : Rules.Condition` - for the same reason: the
        // heading says "references", so the kind is already stated once.
        notations.Add(StructureConceptKeys.ReferenceDecl, ed => ed.Sequence(
            ed.Line(
                ed.Property("name"),
                ed.Space,
                ed.Punct(":"),
                ed.Space,
                ed.Property("targetConceptKey")),
            ed.Indent(ed.Line(
                ed.Keyword("own subtree:"),
                ed.Space,
                ed.Property("allowOwnSubtree")))));

        notations.Add(StructureConceptKeys.Enumeration, ed => ed.Indent(ed.Indent(ed.Sequence(
            ed.Line(ed.Keyword("enum"), ed.Space, ed.Property(StructureLanguage.KeySlot)),
            ed.Indent(ed.Children("symbols", NoSymbols, stacked: true))))));

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
