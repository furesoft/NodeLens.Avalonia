using Projectional.Core;
using Projectional.Language;

namespace Projectional.CSharp.Notation;

/// <summary>How a C# document is drawn: the notation of every concept of the base language.</summary>
/// <remarks>
/// <para>
/// The drawing is the C# a reader expects, which is the point of a base language being *projected* rather than
/// imported: a block's braces stand on their own lines at the indentation of the word that opened them, a member list
/// puts each member on a line of its own, and a parameter or argument list stays on the signature's line. A block
/// that has to appear inside a line - a method's body after its signature - is drawn in a line of its own, because a
/// vertical child inside a line costs the line its extra rows.
/// </para>
/// <para>
/// Two lists are hidden while they are empty, and both owe the user a second way in: an empty parameter or argument
/// list draws nothing, so <c>Greet()</c> reads as C#, and <c>Add a parameter</c> / <c>Add an argument</c> create the
/// first entry. The alternative - keeping the slot visible - would draw <c>Greet( )</c>, because layout keeps one
/// space for every empty slot so the caret has somewhere to stand.
/// </para>
/// </remarks>
public static class CSharpNotations
{
    /// <summary>Adds the notation of every concept of the language to <paramref name="notations"/>.</summary>
    /// <param name="notations">The registry to add to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notations"/> is <see langword="null"/>.</exception>
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        var ed = NotationEditor.Create();

        // A file is its namespace, drawn the way the namespace draws itself.
        // A file is its imports, each on its own row, then the namespace. The imports are wrapped in a when-filled
        // block so a file without them draws nothing where they would be - an empty row would push the namespace
        // down for no reason.
        //
        // The imports take a trailing gap, which is the one position a group boundary cannot describe: the blank line
        // the convention wants separates the last directive from the namespace declaration rather than two of the
        // directives from each other. It sits inside the when-filled block, so a file with no imports gets no blank
        // row in front of its namespace.
        notations.Add(
            CSharpConceptKeys.File,
            ed => ed.Sequence(
                ed.WhenFilledBlock(
                    CSharpConceptKeys.UsingsSlot,
                    ed.Children(
                        CSharpConceptKeys.UsingsSlot,
                        string.Empty,
                        stacked: true,
                        gaps: ListGap.AfterLast)),
                ed.Line(ed.ChildInline(CSharpConceptKeys.NamespaceSlot, "<no namespace>"))));

        notations.Add(CSharpConceptKeys.Using, ed => ed.Line(
            ed.Keyword("using"),
            ed.Space,
            ed.Property(CSharpLanguage.NameSlot),
            ed.Punct(";")));

        // A behaviour class draws what it generates, the attachment comment included: the header line is the model's
        // statement about *which* concept this class is for, and the generated file has to carry that too, because the
        // host's hand-written partial beside it says nothing about the model it belongs to.
        notations.Add(CSharpConceptKeys.Behaviour, ed => ed.Sequence(
            ed.Line(
                ed.Punct("//"),
                ed.Space,
                ed.Keyword("behaviour"),
                ed.Space,
                ed.Property(CSharpConceptKeys.ConceptKeySlot)),
            ed.Line(
                ed.Keyword("partial"),
                ed.Space,
                ed.Keyword("class"),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot)),
            ed.Line(ed.Punct("{")),
            ed.Indent(ed.Children(
                CSharpConceptKeys.MembersSlot,
                "<no methods>",
                stacked: true,
                gaps: ListGap.BetweenGroups)),
            ed.Line(ed.Punct("}"))));

        notations.Add(CSharpConceptKeys.TypeReference, ed => ed.Line(ed.Property(CSharpLanguage.NameSlot)));
        notations.Add(CSharpConceptKeys.IntLiteral, ed => ed.Line(ed.Property(CSharpLanguage.ValueProperty)));
        notations.Add(CSharpConceptKeys.BoolLiteral, ed => ed.Line(ed.Property(CSharpLanguage.ValueProperty)));

        // The long's `L` is drawn beside the value rather than stored in it, so the number stays a number. A double
        // needs nothing: `3.14` already says what it is, and an integral value is legal there.
        notations.Add(CSharpConceptKeys.LongLiteral, ed => ed.Line(
            ed.Property(CSharpLanguage.ValueProperty),
            ed.Punct("L")));
        notations.Add(CSharpConceptKeys.DoubleLiteral, ed => ed.Line(ed.Property(CSharpLanguage.ValueProperty)));

        // `null` has no slot to draw, so the notation is the word and nothing else.
        notations.Add(CSharpConceptKeys.NullLiteral, ed => ed.Line(ed.Keyword("null")));
        notations.Add(CSharpConceptKeys.This, ed => ed.Line(ed.Keyword("this")));
        // A reference cell: it draws what the name means, and falls back to the text the user typed while nothing
        // resolves - so an unresolved name is a dangling reference rather than a second kind of name.
        notations.Add(CSharpConceptKeys.Name, ed => ed.Line(ed.Reference(CSharpConceptKeys.TargetSlot, unsetText: "<name>")));

        notations.Add(CSharpConceptKeys.StringLiteral, ed => ed.Line(
            ed.Punct("\""),
            ed.Property(CSharpLanguage.ValueProperty),
            ed.Punct("\"")));

        notations.Add(CSharpConceptKeys.MemberAccess, ed => ed.Line(
            ed.ChildInline("target"),
            ed.Punct("."),
            ed.Property(CSharpLanguage.MemberSlot)));

        notations.Add(CSharpConceptKeys.Call, ed => ed.Line(
            ed.ChildInline("callee"),
            ed.Punct("("),
            ed.WhenFilled(CSharpConceptKeys.ArgumentsSlot, ed.InlineCollection(CSharpConceptKeys.ArgumentsSlot, ed.Punct(","), string.Empty)),
            ed.Punct(")")));

        notations.Add(CSharpConceptKeys.Binary, ed => ed.Line(
            ed.ChildInline("left"),
            ed.Space,
            ed.Property(CSharpLanguage.OperatorSlot),
            ed.Space,
            ed.ChildInline("right")));

        notations.Add(CSharpConceptKeys.Parenthesized, ed => ed.Line(
            ed.Punct("("),
            ed.ChildInline("inner"),
            ed.Punct(")")));

        // A lambda always draws its parentheses, including the one-parameter form C# lets you shorten: `(item) => item`
        // is the same program as `item => item`, and the notation has no way to say "exactly one" - so it says the
        // thing that is always right rather than the thing that is usually prettier. The list is inside a `WhenFilled`
        // so that an empty parameter list draws `()` rather than the one space an inline collection keeps for the caret.
        notations.Add(CSharpConceptKeys.Lambda, ed => ed.Line(
            ed.Punct("("),
            ed.WhenFilled(
                CSharpConceptKeys.ParametersSlot,
                ed.InlineCollection(CSharpConceptKeys.ParametersSlot, ed.Punct(","), string.Empty)),
            ed.Punct(")"),
            ed.Space,
            ed.Punct("=>"),
            ed.Space,
            ed.ChildInline(CSharpConceptKeys.BodySlot)));

        notations.Add(CSharpConceptKeys.LambdaParameter, ed => ed.Line(ed.Property(CSharpLanguage.NameSlot)));

        notations.Add(CSharpConceptKeys.New, ed => ed.Line(
            ed.Keyword("new"),
            ed.Space,
            ed.ChildInline("type"),
            ed.Punct("("),
            ed.WhenFilled(CSharpConceptKeys.ArgumentsSlot, ed.InlineCollection(CSharpConceptKeys.ArgumentsSlot, ed.Punct(","), string.Empty)),
            ed.Punct(")")));

        // A collection expression: `[1, 2, 3]`, and `[]` when it holds nothing, which is a real value in C# rather
        // than an unfinished one - so the list is hidden while empty and the brackets stand alone.
        notations.Add(CSharpConceptKeys.CollectionExpression, ed => ed.Line(
            ed.Punct("["),
            ed.WhenFilled(CSharpConceptKeys.ElementsSlot, ed.InlineCollection(CSharpConceptKeys.ElementsSlot, ed.Punct(","), string.Empty)),
            ed.Punct("]")));

        // Three expressions whose subject is a type. The type is drawn as a child cell, so it is a node the caret can
        // enter and the type system can read, exactly as a field's type is; the punctuation is the only thing that
        // tells the reader which of the three they are looking at.
        notations.Add(CSharpConceptKeys.Cast, ed => ed.Line(
            ed.Punct("("),
            ed.ChildInline("type"),
            ed.Punct(")"),
            ed.ChildInline(CSharpConceptKeys.OperandSlot)));

        notations.Add(CSharpConceptKeys.Is, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Space,
            ed.Keyword("is"),
            ed.Space,
            ed.ChildInline("type")));

        // `as` is `is` with the value handed back: same operand, same type, the word in the middle.
        notations.Add(CSharpConceptKeys.As, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Space,
            ed.Keyword("as"),
            ed.Space,
            ed.ChildInline("type")));

        notations.Add(CSharpConceptKeys.Index, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.TargetSlot),
            ed.Punct("["),
            ed.ChildInline(CSharpConceptKeys.IndexSlot),
            ed.Punct("]")));

        // The node API, in the shape MPS writes it: a type carries the concept between angle brackets, a thing the
        // node has of its own is a dotted word, and a part addressed by the slot's name uses a slash on either side of
        // the word that says which read it is. The two forms of read are deliberately told apart by punctuation rather
        // than by their spelling alone, because one of them takes a name and the other does not.
        notations.Add(CSharpConceptKeys.NodeType, ed => ed.Line(
            ed.Keyword("node"),
            ed.Punct("<"),
            ed.Property(CSharpLanguage.NameSlot),
            ed.Punct(">")));

        // The enum pair. Both name an enumeration of the *language being defined*, so both generate text: a symbol is
        // stored as its name, and that is what a behaviour compares against. A member type is drawn in the brackets MPS
        // uses for it; a member value is addressed with slashes, and the dot in front of the symbol is MPS's way of
        // saying "inside that enumeration".
        notations.Add(CSharpConceptKeys.EnumMemberType, ed => ed.Line(
            ed.Keyword("enummember"),
            ed.Punct("<"),
            ed.Property(CSharpLanguage.NameSlot),
            ed.Punct(">")));

        notations.Add(CSharpConceptKeys.EnumValue, ed => ed.Line(
            ed.Keyword("enum"),
            ed.Punct("/"),
            ed.Property(CSharpLanguage.EnumSlot),
            ed.Punct("/."),
            ed.Property(CSharpLanguage.MemberSlot)));

        notations.Add(CSharpConceptKeys.NodeName, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("."),
            ed.Keyword("name")));

        notations.Add(CSharpConceptKeys.NodeParent, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("."),
            ed.Keyword("parent")));

        notations.Add(CSharpConceptKeys.NodeIndex, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("."),
            ed.Keyword("index")));

        notations.Add(CSharpConceptKeys.NodeConcept, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("."),
            ed.Keyword("concept")));

        notations.Add(CSharpConceptKeys.NodeChildren, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("/"),
            ed.Keyword("children"),
            ed.Punct("/"),
            ed.Property(CSharpLanguage.SlotNameSlot)));

        notations.Add(CSharpConceptKeys.NodeChild, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("/"),
            ed.Keyword("child"),
            ed.Punct("/"),
            ed.Property(CSharpLanguage.SlotNameSlot)));

        notations.Add(CSharpConceptKeys.NodeProperty, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("/"),
            ed.Keyword("property"),
            ed.Punct("/"),
            ed.Property(CSharpLanguage.SlotNameSlot)));

        notations.Add(CSharpConceptKeys.NodeReference, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("/"),
            ed.Keyword("reference"),
            ed.Punct("/"),
            ed.Property(CSharpLanguage.SlotNameSlot)));

        notations.Add(CSharpConceptKeys.NodeResolveInfo, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.OperandSlot),
            ed.Punct("/"),
            ed.Keyword("resolveInfo"),
            ed.Punct("/"),
            ed.Property(CSharpLanguage.SlotNameSlot)));

        // A namespace, in either of C# 10's two forms, each spelled out in its own group of rows: the file-scoped one
        // ends the header with a semicolon and puts the classes at the same level, the block one opens a brace, moves
        // them in and closes it. The group the model did not choose leaves no row at all, which is what a conditional
        // block is for - an empty group would still take the line layout reserves for it.
        //
        // The type list is groups with no tight concept at all, so every type is separated from the next by a blank
        // line - unlike the class's member list, where a run of fields stays together. That is the difference between
        // the two conventions: members of one kind belong together, types of any kind never do.
        notations.Add(CSharpConceptKeys.Namespace, ed => ed.Sequence(
            ed.ConditionalBlock(
                CSharpLanguage.IsFileScopedSlot,
                "true",
                ed.Line(
                    ed.Keyword("namespace"),
                    ed.Space,
                    ed.Property(CSharpLanguage.NameSlot),
                    ed.Punct(";")),
                ed.Children(
                    CSharpConceptKeys.ClassesSlot,
                    "<no classes>",
                    stacked: true,
                    gaps: ListGap.BetweenGroups | ListGap.BeforeFirst)),
            ed.ConditionalBlock(
                CSharpLanguage.IsFileScopedSlot,
                "false",
                ed.Line(
                    ed.Keyword("namespace"),
                    ed.Space,
                    ed.Property(CSharpLanguage.NameSlot)),
                ed.Line(ed.Punct("{")),
                ed.Indent(ed.Children(
                    CSharpConceptKeys.ClassesSlot,
                    "<no classes>",
                    stacked: true,
                    gaps: ListGap.BetweenGroups)),
                ed.Line(ed.Punct("}")))));

        // A class: the same shape, with its members between the braces. Fields are the tight kind, so a run of fields
        // stays together and every other member is separated by a blank line - the .NET convention's "a blank line
        // between members, except inside a block of fields". The drawing and the generator read the same set, so the
        // two cannot disagree about where a group ends.
        notations.Add(CSharpConceptKeys.Class, ed => ed.Sequence(
            ed.Line(
                ed.Property(CSharpLanguage.VisibilitySlot),
                ed.Space,
                ed.Conditional(CSharpLanguage.IsStaticSlot, "true", ed.Keyword("static"), ed.Space),
                ed.Keyword("class"),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot),
                ed.WhenFilled(CSharpConceptKeys.InheritanceSlot, ed.Space, ed.ChildInline(CSharpConceptKeys.InheritanceSlot))),
            ed.Line(ed.Punct("{")),
            ed.Indent(ed.Children(
                CSharpConceptKeys.MembersSlot,
                "<no members>",
                stacked: true,
                gaps: ListGap.BetweenGroups,
                tightConcepts: CSharpMemberGroups.Tight)),
            ed.Line(ed.Punct("}"))));

        // `: Base, IFoo` - the colon belongs to the list, so a class that inherits nothing draws no colon.
        notations.Add(CSharpConceptKeys.Inheritance, ed => ed.Line(
            ed.Punct(":"),
            ed.Space,
            ed.InlineCollection(CSharpConceptKeys.EntriesSlot, ed.Punct(","))));

        notations.Add(CSharpConceptKeys.InheritanceEntry, ed => ed.Line(
            ed.Reference(CSharpConceptKeys.TargetSlot, unsetText: "<type>")));
        // An interface is a class without members that have bodies: the header, the brace, the signatures.
        notations.Add(CSharpConceptKeys.Interface, ed => ed.Sequence(
            ed.Line(
                ed.Property(CSharpLanguage.VisibilitySlot),
                ed.Space,
                ed.Keyword("interface"),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot)),
            ed.Line(ed.Punct("{")),
            ed.Indent(ed.Children(CSharpConceptKeys.MembersSlot, "<no members>", stacked: true)),
            ed.Line(ed.Punct("}"))));

        // An enum: a header and its values, comma-separated in a row. The values are a collection rather than a
        // children list because a comma between them is punctuation that belongs to the list, not to the value - and
        // the collection cell draws in a row, so the generator joins them with a comma and a space to match.
        notations.Add(CSharpConceptKeys.Enum, ed => ed.Sequence(
            ed.Line(
                ed.Property(CSharpLanguage.VisibilitySlot),
                ed.Space,
                ed.Keyword("enum"),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot)),
            ed.Line(ed.Punct("{")),
            ed.Indent(ed.Collection(CSharpConceptKeys.MembersSlot, ed.Punct(","), "<no values>")),
            ed.Line(ed.Punct("}"))));

        // One value: its name and nothing else. The comma belongs to the list, so the value draws no punctuation.
        notations.Add(CSharpConceptKeys.EnumMember, ed => ed.Line(ed.Property(CSharpLanguage.NameSlot)));

        // A field: one line, and the `= value` part only while there is an initializer.
        notations.Add(CSharpConceptKeys.Field, ed => ed.Line(
            ed.Property(CSharpLanguage.VisibilitySlot),
            ed.Space,
            ed.ChildInline("type"),
            ed.Space,
            ed.Property(CSharpLanguage.NameSlot),
            ed.WhenFilled(
                CSharpConceptKeys.ValueSlot,
                ed.Space,
                ed.Punct("="),
                ed.Space,
                ed.ChildInline(CSharpConceptKeys.ValueSlot)),
            ed.Punct(";")));

        // A method: the signature with its parameter list inline, then the body on the lines below it.
        notations.Add(CSharpConceptKeys.Method, ed => ed.Sequence(
            ed.Line(
                ed.Property(CSharpLanguage.VisibilitySlot),
                ed.Space,
                ed.Conditional(CSharpLanguage.IsStaticSlot, "true", ed.Keyword("static"), ed.Space),
                ed.ChildInline("returnType"),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot),
                ed.Punct("("),
                ed.WhenFilled(CSharpConceptKeys.ParametersSlot, ed.InlineCollection(CSharpConceptKeys.ParametersSlot, ed.Punct(","), string.Empty)),
                ed.Punct(")")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.BodySlot, "<no body>"))));

        // A constructor: the signature without a return type, then the body - the shape a method has, one part shorter.
        notations.Add(CSharpConceptKeys.Constructor, ed => ed.Sequence(
            ed.Line(
                ed.Property(CSharpLanguage.VisibilitySlot),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot),
                ed.Punct("("),
                ed.WhenFilled(CSharpConceptKeys.ParametersSlot, ed.InlineCollection(CSharpConceptKeys.ParametersSlot, ed.Punct(","), string.Empty)),
                ed.Punct(")")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.BodySlot, "<no body>"))));

        // A property in its two shapes. The auto-property stays on one line, because that is what it is: the accessors
        // are words, not blocks. With bodies they are blocks, so the property takes the same shape a method has - the
        // header, then the accessor and its block below it. The setter is a conditional block of its own, which is what
        // makes `{ get { ... } }` one boolean away from `{ get { ... } set { ... } }`.
        notations.Add(CSharpConceptKeys.Property, ed => ed.Sequence(
            ed.ConditionalBlock(
                CSharpConceptKeys.HasBodiesSlot,
                "false",
                ed.Line(
                    ed.Property(CSharpLanguage.VisibilitySlot),
                    ed.Space,
                    ed.ChildInline("type"),
                    ed.Space,
                    ed.Property(CSharpLanguage.NameSlot),
                    ed.Space,
                    ed.Punct("{"),
                    ed.Space,
                    ed.Keyword("get;"),
                    ed.Conditional(CSharpConceptKeys.HasSetterSlot, "true", ed.Space, ed.Keyword("set;")),
                    ed.Space,
                    ed.Punct("}"))),
            ed.ConditionalBlock(
                CSharpConceptKeys.HasBodiesSlot,
                "true",
                ed.Line(
                    ed.Property(CSharpLanguage.VisibilitySlot),
                    ed.Space,
                    ed.ChildInline("type"),
                    ed.Space,
                    ed.Property(CSharpLanguage.NameSlot)),
                ed.Line(ed.Punct("{")),
                ed.Indent(
                    ed.Line(ed.Keyword("get")),
                    ed.Line(ed.ChildInline(CSharpConceptKeys.GetterSlot, "<no getter>")),
                    ed.ConditionalBlock(
                        CSharpConceptKeys.HasSetterSlot,
                        "true",
                        ed.Line(ed.Keyword("set")),
                        ed.Line(ed.ChildInline(CSharpConceptKeys.SetterSlot, "<no setter>")))),
                ed.Line(ed.Punct("}")))));

        // An indexer: the type, then `this` and the parameters in brackets, then the accessors - the same shape a
        // property with bodies has, with the parameters where the name would be. The setter is a WhenFilledBlock,
        // because a get-only indexer leaves no row for a setter that is not there.
        notations.Add(CSharpConceptKeys.Indexer, ed => ed.Sequence(
            ed.Line(
                ed.Property(CSharpLanguage.VisibilitySlot),
                ed.Space,
                ed.ChildInline("type"),
                ed.Space,
                ed.Keyword("this"),
                ed.Punct("["),
                ed.InlineCollection(CSharpConceptKeys.ParametersSlot, ed.Punct(",")),
                ed.Punct("]")),
            ed.Line(ed.Punct("{")),
            ed.Indent(
                ed.Line(ed.Keyword("get")),
                ed.Line(ed.ChildInline(CSharpConceptKeys.GetterSlot, "<no getter>")),
                ed.WhenFilledBlock(
                    CSharpConceptKeys.SetterSlot,
                    ed.Line(ed.Keyword("set")),
                    ed.Line(ed.ChildInline(CSharpConceptKeys.SetterSlot)))),
            ed.Line(ed.Punct("}"))));
        // A signature: a method's header and nothing after it.
        notations.Add(CSharpConceptKeys.Signature, ed => ed.Line(
            ed.Property(CSharpLanguage.VisibilitySlot),
            ed.Space,
            ed.ChildInline("returnType"),
            ed.Space,
            ed.Property(CSharpLanguage.NameSlot),
            ed.Punct("("),
            ed.WhenFilled(CSharpConceptKeys.ParametersSlot, ed.InlineCollection(CSharpConceptKeys.ParametersSlot, ed.Punct(","), string.Empty)),
            ed.Punct(")"),
            ed.Punct(";")));
        notations.Add(CSharpConceptKeys.Parameter, ed => ed.Line(
            ed.ChildInline("type"),
            ed.Space,
            ed.Property(CSharpLanguage.NameSlot)));

        notations.Add(CSharpConceptKeys.Block, ed => ed.Sequence(
            ed.Line(ed.Punct("{")),
            ed.Indent(ed.Children(CSharpConceptKeys.StatementsSlot, "<no statements>", stacked: true)),
            ed.Line(ed.Punct("}"))));

        notations.Add(CSharpConceptKeys.ExpressionStatement, ed => ed.Line(
            ed.ChildInline("expression"),
            ed.Punct(";")));

        // Two one-sided conditionals rather than one either/or, because an inline conditional draws its content while
        // the property holds the value and nothing otherwise - it has no else branch. The property is materialized with
        // its default when the node is created, so exactly one of the two ever draws.
        notations.Add(CSharpConceptKeys.LocalDeclaration, ed => ed.Line(
            ed.Conditional(CSharpLanguage.IsVarSlot, "true", ed.Keyword("var")),
            ed.Conditional(CSharpLanguage.IsVarSlot, "false", ed.ChildInline("type")),
            ed.Space,
            ed.Property(CSharpLanguage.NameSlot),
            ed.WhenFilled(
                CSharpConceptKeys.ValueSlot,
                ed.Space,
                ed.Punct("="),
                ed.Space,
                ed.ChildInline(CSharpConceptKeys.ValueSlot)),
            ed.Punct(";")));

        // The same shape as a local declaration without its `;`: the `for` draws the separator, so the header keeps
        // both of them whatever the initializer holds.
        notations.Add(CSharpConceptKeys.ForInitializer, ed => ed.Line(
            ed.Conditional(CSharpLanguage.IsVarSlot, "true", ed.Keyword("var")),
            ed.Conditional(CSharpLanguage.IsVarSlot, "false", ed.ChildInline("type")),
            ed.Space,
            ed.Property(CSharpLanguage.NameSlot),
            ed.WhenFilled(
                CSharpConceptKeys.ValueSlot,
                ed.Space,
                ed.Punct("="),
                ed.Space,
                ed.ChildInline(CSharpConceptKeys.ValueSlot))));

        // The operator is a property, so `count = 1` and `count += 1` are one node with one difference.
        notations.Add(CSharpConceptKeys.Assignment, ed => ed.Line(
            ed.ChildInline("target"),
            ed.Space,
            ed.Property(CSharpLanguage.AssignmentOperatorSlot),
            ed.Space,
            ed.ChildInline(CSharpConceptKeys.ValueSlot),
            ed.Punct(";")));

        // Two conditionals in one line, of which exactly one draws: `!ready` and `count++` are one concept with one
        // boolean, because the operator is the same and only its side differs.
        notations.Add(CSharpConceptKeys.Unary, ed => ed.Line(
            ed.Conditional(
                CSharpLanguage.IsPostfixSlot,
                "false",
                ed.Property(CSharpLanguage.UnaryOperatorSlot),
                ed.ChildInline(CSharpConceptKeys.OperandSlot)),
            ed.Conditional(
                CSharpLanguage.IsPostfixSlot,
                "true",
                ed.ChildInline(CSharpConceptKeys.OperandSlot),
                ed.Property(CSharpLanguage.UnaryOperatorSlot))));

        // `for (int i = 0; i < 3; i++)` - both separators belong to the header, which is why the shape the
        // documentation spells is the line the editor draws. The initializer draws no `;` of its own and the increment
        // is an expression, so neither can close the header early.
        notations.Add(CSharpConceptKeys.For, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("for"),
                ed.Space,
                ed.Punct("("),
                ed.ChildInline(CSharpConceptKeys.InitializerSlot),
                ed.Punct(";"),
                ed.Space,
                ed.ChildInline(CSharpConceptKeys.ConditionSlot),
                ed.Punct(";"),
                ed.Space,
                ed.ChildInline(CSharpConceptKeys.IncrementSlot),
                ed.Punct(")")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.BodySlot, "<no body>"))));

        notations.Add(CSharpConceptKeys.Conditional, ed => ed.Line(
            ed.ChildInline(CSharpConceptKeys.ConditionSlot),
            ed.Space,
            ed.Punct("?"),
            ed.Space,
            ed.ChildInline(CSharpLanguage.WhenTrueSlot),
            ed.Space,
            ed.Punct(":"),
            ed.Space,
            ed.ChildInline(CSharpLanguage.WhenFalseSlot)));

        // An `if`: the condition, its block, and the else branch in a group of rows that is absent while there is none.
        notations.Add(CSharpConceptKeys.If, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("if"),
                ed.Space,
                ed.Punct("("),
                ed.ChildInline(CSharpConceptKeys.ConditionSlot),
                ed.Punct(")")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.ThenSlot, "<no then>")),
            ed.WhenFilledBlock(
                CSharpConceptKeys.OtherwiseSlot,
                ed.Line(ed.Keyword("else")),
                ed.Line(ed.ChildInline(CSharpConceptKeys.OtherwiseSlot)))));

        // A loop: the header on its line, the body on the lines below it - the same shape `if` has, which is what makes
        // a nest of them read as the code it is.
        notations.Add(CSharpConceptKeys.While, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("while"),
                ed.Space,
                ed.Punct("("),
                ed.ChildInline(CSharpConceptKeys.ConditionSlot),
                ed.Punct(")")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.BodySlot, "<no body>"))));

        notations.Add(CSharpConceptKeys.ForEach, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("foreach"),
                ed.Space,
                ed.Punct("("),
                ed.ChildInline("type"),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot),
                ed.Space,
                ed.Keyword("in"),
                ed.Space,
                ed.ChildInline(CSharpConceptKeys.SourceSlot),
                ed.Punct(")")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.BodySlot, "<no body>"))));

        notations.Add(CSharpConceptKeys.Break, ed => ed.Line(ed.Keyword("break"), ed.Punct(";")));
        notations.Add(CSharpConceptKeys.Continue, ed => ed.Line(ed.Keyword("continue"), ed.Punct(";")));
        notations.Add(CSharpConceptKeys.Return, ed => ed.Line(
            ed.Keyword("return"),
            ed.WhenFilled(CSharpConceptKeys.ValueSlot, ed.Space, ed.ChildInline(CSharpConceptKeys.ValueSlot)),
            ed.Punct(";")));

        notations.Add(CSharpConceptKeys.Throw, ed => ed.Line(
            ed.Keyword("throw"),
            ed.Space,
            ed.ChildInline(CSharpConceptKeys.ValueSlot),
            ed.Punct(";")));

        // A try: its block, then the handlers one per line and the final block if there is one. No indent around the
        // handlers - `catch` and `finally` stand at the same column as `try`, which is how C# reads, and the blocks
        // below them draw their own braces.
        notations.Add(CSharpConceptKeys.Try, ed => ed.Sequence(
            ed.Line(ed.Keyword("try")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.BodySlot)),
            ed.Children(CSharpConceptKeys.CatchesSlot, "<no catch>", stacked: true),
            ed.WhenFilledBlock(
                CSharpConceptKeys.FinallySlot,
                ed.Line(ed.Keyword("finally")),
                ed.Line(ed.ChildInline(CSharpConceptKeys.FinallySlot)))));

        notations.Add(CSharpConceptKeys.Catch, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("catch"),
                ed.Space,
                ed.Punct("("),
                ed.ChildInline("type"),
                ed.Space,
                ed.Property(CSharpLanguage.NameSlot),
                ed.Punct(")")),
            ed.Line(ed.ChildInline(CSharpConceptKeys.BodySlot))));

        // A switch: the value in parentheses, then its sections one per row. The sections are a children list, so
        // an empty switch draws the placeholder the list declares and the caret can enter it.
        notations.Add(CSharpConceptKeys.Switch, ed => ed.Sequence(
            ed.Line(
                ed.Keyword("switch"),
                ed.Space,
                ed.Punct("("),
                ed.ChildInline(CSharpConceptKeys.SubjectSlot),
                ed.Punct(")")),
            ed.Line(ed.Punct("{")),
            ed.Indent(ed.Children(CSharpConceptKeys.SectionsSlot, "<no sections>", stacked: true)),
            ed.Line(ed.Punct("}"))));

        // One section: `case value:` or `default:`, then what it runs indented beneath. Two conditional blocks
        // rather than one "when filled" cell, because the two forms differ in a keyword and not only in a slot.
        notations.Add(CSharpConceptKeys.SwitchSection, ed => ed.Sequence(
            ed.ConditionalBlock(CSharpConceptKeys.IsDefaultSlot, "true",
                ed.Line(ed.Keyword("default"), ed.Punct(":"))),
            ed.ConditionalBlock(CSharpConceptKeys.IsDefaultSlot, "false",
                ed.Line(
                    ed.Keyword("case"),
                    ed.Space,
                    ed.ChildInline(CSharpConceptKeys.LabelSlot),
                    ed.Punct(":"))),
            ed.Indent(ed.Children(CSharpConceptKeys.StatementsSlot, "<no statements>", stacked: true))));
    }

    /// <summary>A registry holding only the base language, built over <paramref name="concepts"/>.</summary>
    /// <param name="concepts">The registry the notations are checked against.</param>
    /// <exception cref="ArgumentNullException"><paramref name="concepts"/> is <see langword="null"/>.</exception>
    public static NotationRegistry CreateNotations(ConceptRegistry concepts)
    {
        ArgumentNullException.ThrowIfNull(concepts);

        var notations = new NotationRegistry(concepts);

        AddTo(notations);

        return notations;
    }
}
