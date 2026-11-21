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
        notations.Add(
            CSharpConceptKeys.File,
            ed => ed.Line(ed.ChildInline(CSharpConceptKeys.NamespaceSlot, "<no namespace>")));

        notations.Add(CSharpConceptKeys.TypeReference, ed => ed.Line(ed.Property(CSharpLanguage.NameSlot)));
        notations.Add(CSharpConceptKeys.IntLiteral, ed => ed.Line(ed.Property(CSharpLanguage.ValueProperty)));
        notations.Add(CSharpConceptKeys.BoolLiteral, ed => ed.Line(ed.Property(CSharpLanguage.ValueProperty)));
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

        // A namespace, in either of C# 10's two forms, each spelled out in its own group of rows: the file-scoped one
        // ends the header with a semicolon and puts the classes at the same level, the block one opens a brace, moves
        // them in and closes it. The group the model did not choose leaves no row at all, which is what a conditional
        // block is for - an empty group would still take the line layout reserves for it.
        notations.Add(CSharpConceptKeys.Namespace, ed => ed.Sequence(
            ed.ConditionalBlock(
                CSharpLanguage.IsFileScopedSlot,
                "true",
                ed.Line(
                    ed.Keyword("namespace"),
                    ed.Space,
                    ed.Property(CSharpLanguage.NameSlot),
                    ed.Punct(";")),
                ed.Children(CSharpConceptKeys.ClassesSlot, "<no classes>", stacked: true)),
            ed.ConditionalBlock(
                CSharpLanguage.IsFileScopedSlot,
                "false",
                ed.Line(
                    ed.Keyword("namespace"),
                    ed.Space,
                    ed.Property(CSharpLanguage.NameSlot)),
                ed.Line(ed.Punct("{")),
                ed.Indent(ed.Children(CSharpConceptKeys.ClassesSlot, "<no classes>", stacked: true)),
                ed.Line(ed.Punct("}")))));

        // A class: the same shape, with its members between the braces.
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
            ed.Indent(ed.Children(CSharpConceptKeys.MembersSlot, "<no members>", stacked: true)),
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

        // An auto-property on one line, because that is what it is: the accessors are words, not blocks.
        notations.Add(CSharpConceptKeys.Property, ed => ed.Line(
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
            ed.Punct("}")));
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

        notations.Add(CSharpConceptKeys.LocalDeclaration, ed => ed.Line(
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

        // The operator is a property, so `count = 1` and `count += 1` are one node with one difference.
        notations.Add(CSharpConceptKeys.Assignment, ed => ed.Line(
            ed.ChildInline("target"),
            ed.Space,
            ed.Property(CSharpLanguage.AssignmentOperatorSlot),
            ed.Space,
            ed.ChildInline(CSharpConceptKeys.ValueSlot),
            ed.Punct(";")));

        notations.Add(CSharpConceptKeys.Unary, ed => ed.Line(
            ed.Property(CSharpLanguage.UnaryOperatorSlot),
            ed.ChildInline(CSharpConceptKeys.OperandSlot)));

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
