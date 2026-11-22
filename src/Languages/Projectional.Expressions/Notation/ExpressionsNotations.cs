using Projectional.Language;

namespace Projectional.Expressions;

/// <summary>
/// The notation of every concept <see cref="ExpressionsLanguage"/> declares.
/// </summary>
/// <remarks>
/// <para>
/// Every notation is a single <see cref="NotationEditor.Line"/>, which is what lets a host place an
/// expression inline in its own line: <c>when amount &gt; 1000</c> is one line of one rule, not a
/// block of its own. A language that gave its expressions a multi-line notation could only ever place
/// them as children on their own lines.
/// </para>
/// <para>
/// The parenthesized form draws its parentheses instead of hiding them, even though the tree already
/// carries the grouping: the text a user reads should be the text the tree means, and a tree whose
/// rendering is <c>1 + 2 * 3</c> whether it groups the addition or the multiplication is a projection
/// that loses information.
/// </para>
/// </remarks>
internal static class ExpressionsNotations
{
    public static void AddTo(NotationRegistry notations)
    {
        ArgumentNullException.ThrowIfNull(notations);

        notations.Add(ExpressionsConceptKeys.IntLiteral, ed => ed.Line(ed.Property("value")));

        notations.Add(ExpressionsConceptKeys.BoolLiteral, ed => ed.Line(ed.Property("value")));

        notations.Add(ExpressionsConceptKeys.StringLiteral, ed => ed.Line(
            ed.Punct("\""),
            ed.Property("value"),
            ed.Punct("\"")));

        notations.Add(ExpressionsConceptKeys.Name, ed => ed.Line(ed.Property("name")));

        // The operator is an empty cell until someone picks one - a fresh Binary from Enter, or a Unary that a
        // trigger did not fill - so the cell says what it waits for instead of drawing a blank of one space.
        notations.Add(ExpressionsConceptKeys.Binary, ed => ed.Line(
            ed.ChildInline("left"),
            ed.Space,
            ed.Property("operator", emptyText: "<<operator>>"),
            ed.Space,
            ed.ChildInline("right")));

        notations.Add(ExpressionsConceptKeys.Unary, ed => ed.Line(
            ed.Property("operator", emptyText: "<<operator>>"),
            ed.ChildInline("operand")));

        notations.Add(ExpressionsConceptKeys.Parenthesized, ed => ed.Line(
            ed.Punct("("),
            ed.ChildInline("inner"),
            ed.Punct(")")));

        // One line, because every expression notation has to be one: an inline child refuses anything else.
        notations.Add(ExpressionsConceptKeys.If, ed => ed.Line(
            ed.Keyword("if"),
            ed.Space,
            ed.ChildInline("condition"),
            ed.Space,
            ed.Keyword("then"),
            ed.Space,
            ed.ChildInline("then"),
            ed.Space,
            ed.Keyword("else"),
            ed.Space,
            ed.ChildInline("else")));

        // `f(x, y) = x ^ y`, one line, because that is how the definition reads as an equation.
        notations.Add(ExpressionsConceptKeys.Function, ed => ed.Line(
            ed.Property("name"),
            ed.Punct("("),
            ed.InlineCollection(ExpressionsLanguage.ParametersSlot, separator: ed.Punct(", ")),
            ed.Punct(")"),
            ed.Space,
            ed.Punct("="),
            ed.Space,
            ed.ChildInline("body")));

        notations.Add(ExpressionsConceptKeys.Parameter, ed => ed.Line(ed.Property("name")));

        notations.Add(ExpressionsConceptKeys.Call, ed => ed.Line(
            ed.Property("callee"),
            ed.Punct("("),
            ed.InlineCollection(ExpressionsLanguage.ArgumentsSlot, separator: ed.Punct(", ")),
            ed.Punct(")")));
    }
}
